global using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using NatureOS.CoreApi.Controllers;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using JsonNet=Newtonsoft.Json.JsonConvert;
using Json=System.Text.Json.JsonSerializer;

var tests=new List<(string,Func<Task>)>();
void Add(string name,Func<Task> run)=>tests.Add((name,run));
foreach(var shape in new[]{"canonical","core","ingestion"}) {
    var s=shape;
    Add(s+" humidity inclusive",async()=> {var f=Fixture(s);var r=await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=50,Max=80}});Ids(r,$"{s}-middle",$"{s}-wet");});
    Add(s+" ph inclusive",async()=> {var f=Fixture(s);var r=await f.Service.GetFungaEventsAsync(new(){pHRange=new(){Min=6,Max=7}});Ids(r,$"{s}-middle",$"{s}-wet");});
    Add(s+" combined AND",async()=> {var f=Fixture(s);var r=await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=60,Max=80},pHRange=new(){Min=6,Max=7}});Ids(r,$"{s}-wet");});
    Add(s+" unknown preserved unfiltered",async()=> {var f=Fixture(s);var r=await f.Service.GetFungaEventsAsync(new());Check(r.Items.Count()==5,"all five source-backed documents must remain readable");Check(r.Items.All(x=>x.EventId.StartsWith(s)),"DTO identity must materialize");});
    Add(s+" zero endpoint",async()=> {var f=Fixture(s);Ids(await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=0,Max=0},pHRange=new(){Min=0,Max=0}}),$"{s}-zero");});
}
Add("one-sided defaults",async()=> {
 var f=Fixture("core");var result=await f.Controller.GetFungaEvents(humidityMin:60,pHMax:7);
 Ids(Ok(result),"core-wet");
 Check(f.Container.Queries.SelectMany(x=>x.Parameters).Any(x=>x.Key.Contains("humidity",StringComparison.OrdinalIgnoreCase)&&Equals(x.Value,100d)),"humidity upper default100 bound");
 Check(f.Container.Queries.SelectMany(x=>x.Parameters).Any(x=>x.Key.Contains("ph",StringComparison.OrdinalIgnoreCase)&&Equals(x.Value,0d)),"ph lower default0 bound");
});
foreach(var value in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {
 var v=value;Add("controller rejects humidity "+v,async()=> {var f=Fixture("canonical");Check((await f.Controller.GetFungaEvents(humidityMin:v)).Result is BadRequestObjectResult,"expected400");Check(f.Container.Reads==0,"no provider reads");});
 Add("service rejects ph "+v,async()=> {var f=Fixture("core");await RejectArgument(()=>f.Service.GetFungaEventsAsync(new(){pHRange=new(){Min=v,Max=7}}));Check(f.Container.Reads==0,"no provider reads");});
}
Add("reversed ranges",async()=> {var f=Fixture("ingestion");Check((await f.Controller.GetFungaEvents(pHMin:9,pHMax:2)).Result is BadRequestObjectResult,"expected400");await RejectArgument(()=>f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=81,Max=80}}));Check(f.Container.Reads==0,"no read on invalid bounds");});
Add("mixed shapes pagination ties and unread-head replay",async()=> {
 var f=Fixture("canonical","core","ingestion");var ids=new List<string>();string? token=null;int pages=0;
 do {var r=await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=50,Max=80},PageSize=2,ContinuationToken=token});ids.AddRange(r.Items.Select(x=>x.EventId));token=r.ContinuationToken;Check(++pages<10,"pagination terminates");}while(token!=null);
 Check(ids.SequenceEqual(new[]{"canonical-wet","core-wet","ingestion-wet","canonical-middle","core-middle","ingestion-middle"}),"stable descending/tie order without duplicate/skipped heads: "+string.Join(',',ids));
 Check(f.Container.Reads<=24,"bounded one-head merge reads");
});
Add("ascending with existing filters",async()=> {var f=Fixture("canonical","core","ingestion");var r=await f.Service.GetFungaEventsAsync(new(){SortOrder="timestamp_asc",SourceDevice="fixture-device",Phylum="fixture-phylum",SubstrateType="fixture-substrate",TemperatureRange=new(){Min=20,Max=21},HumidityRange=new(){Min=0,Max=100},StartTime=new(2020,1,1,0,0,0,DateTimeKind.Utc),EndTime=new(2020,1,3,0,0,0,DateTimeKind.Utc)});Check(r.Items.Select(x=>x.EventId).SequenceEqual(new[]{"canonical-zero","core-zero","ingestion-zero","canonical-middle","core-middle","ingestion-middle"}),"source-backed filters across all shapes");});
Add("empty streams",async()=> {var f=Fixture();var r=await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=1,Max=2}});Check(!r.Items.Any()&&!r.HasMore&&r.ContinuationToken==null,"empty result");Check(f.Container.Reads==3,"one bounded peek per shape");});
Add("empty provider pages",async()=> {var f=Fixture("canonical");f.Container.EmptyPages=2;var r=await f.Service.GetFungaEventsAsync(new(){pHRange=new(){Min=6,Max=7}});Ids(r,"canonical-middle","canonical-wet");});
Add("non-numeric and null environmental values excluded",async()=> {var f=Fixture("canonical");var raw=(JObject)f.Container.Rows[1].DeepClone();raw["event_id"]="fixture-string";raw["references"]!["environment"]!["humidity"]="50";f.Container.Rows.Add(raw);Ids(await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=50,Max=80}}),"canonical-middle","canonical-wet");});
Add("equal aliases routed once",async()=> {var f=Fixture("canonical");foreach(var row in f.Container.Rows){row["kingdomDomain"]=row["kingdom_domain"]!.DeepClone();row["KingdomDomain"]=row["kingdom_domain"]!.DeepClone();}var r=await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=50,Max=80}});Ids(r,"canonical-middle","canonical-wet");});
Add("conflicting aliases explicit503",async()=> {var f=Fixture("core");f.Container.Rows[1]["EventId"]="conflicting-fixture-id";var r=await f.Controller.GetFungaEvents(humidityMin:50,humidityMax:80);Check(r.Result is ObjectResult{StatusCode:503},"schema conflict must not return partial200");});
Add("malformed timestamp explicit503",async()=> {var f=Fixture("ingestion");f.Container.Rows[1]["Timestamp"]="invalid-fixture-time";var r=await f.Controller.GetFungaEvents(humidityMin:50,humidityMax:80);Check(r.Result is ObjectResult{StatusCode:503},"invalid materialization must fail explicitly");});
Add("changed filter cursor rejected before I/O",async()=> {var f=Fixture("canonical","core");var first=await f.Service.GetFungaEventsAsync(new(){PageSize=1,HumidityRange=new(){Min=50,Max=80}});int reads=f.Container.Reads;var r=await f.Controller.GetFungaEvents(humidityMin:0,humidityMax:100,continuationToken:first.ContinuationToken);Check(r.Result is BadRequestObjectResult,"mismatched cursor400");Check(f.Container.Reads==reads,"invalid cursor must not query");});
Add("malformed and legacy cursor400",async()=> {var f=Fixture("canonical");foreach(var token in new[]{"%%%","legacy-provider-token","e30"})Check((await f.Controller.GetFungaEvents(continuationToken:token)).Result is BadRequestObjectResult,"unsupported cursor400");Check(f.Container.Reads==0,"no provider reads");});
Add("provider failure never successful",async()=> {var f=Fixture("core");f.Container.FailRead=true;var r=await f.Controller.GetFungaEvents(humidityMin:50);Check(r.Result is ObjectResult{StatusCode:500},"provider failure should preserve500");});
Add("cancellation forwarded",async()=> {var f=Fixture("canonical");using var c=new CancellationTokenSource();c.Cancel();try{await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=0,Max=100}},c.Token);throw new Exception("expected cancellation");}catch(OperationCanceledException){}Check(f.Container.LastCancellation==c.Token,"actual cancellation token");});

Add("edge nested equal aliases and numeric representations",async()=> {var f=Fixture("canonical");foreach(var row in f.Container.Rows){var env=(JObject)row["references"]!["environment"]!;if(env["ph"]!.Type!=JTokenType.Null)env["pH"]=env["ph"]!.Value<double>();row["References"]=row["references"]!.DeepClone();}Ids(await f.Service.GetFungaEventsAsync(new(){pHRange=new(){Min=6,Max=7}}),"canonical-middle","canonical-wet");});
Add("edge conflicting environmental aliases explicit503",async()=> {var f=Fixture("canonical");f.Container.Rows[1]["references"]!["environment"]!["pH"]=6;Check((await f.Controller.GetFungaEvents(humidityMin:50,humidityMax:80)).Result is ObjectResult{StatusCode:503},"conflicting pH aliases503");});
Add("edge conflicting domain aliases explicit503",async()=> {var f=Fixture("canonical");f.Container.Rows[1]["kingdomDomain"]="FLORA.fixture";Check((await f.Controller.GetFungaEvents(humidityMin:50,humidityMax:80)).Result is ObjectResult{StatusCode:503},"conflicting domain aliases503");});
Add("edge malformed typed data explicit503",async()=> {var f=Fixture("core");f.Container.Rows[1]["references"]!["environment"]!["pH"]="not-a-number";Check((await f.Controller.GetFungaEvents(humidityMin:50,humidityMax:80)).Result is ObjectResult{StatusCode:503},"unfiltered malformed typed field503");});
Add("edge missing timestamp explicit503",async()=> {var f=Fixture("core");f.Container.Rows[1].Remove("timestamp");Check((await f.Controller.GetFungaEvents(humidityMin:50,humidityMax:80)).Result is ObjectResult{StatusCode:503},"returned row without timestamp503");});
Add("edge within-stream tied timestamps replay",async()=> {var f=Fixture("core");f.Container.Rows[1]["timestamp"]=f.Container.Rows[2]["timestamp"]!.DeepClone();var ids=new List<string>();string? cursor=null;do{var r=await f.Service.GetFungaEventsAsync(new(){PageSize=1,HumidityRange=new(){Min=50,Max=80},ContinuationToken=cursor});ids.AddRange(r.Items.Select(x=>x.EventId));cursor=r.ContinuationToken;}while(cursor!=null);Check(ids.SequenceEqual(new[]{"core-middle","core-wet"}),"stable fixture provider continuation order for ties");});
Add("edge page size change preserves cursor",async()=> {var f=Fixture("canonical","core","ingestion");var first=await f.Service.GetFungaEventsAsync(new(){PageSize=1,HumidityRange=new(){Min=50,Max=80}});var second=await f.Service.GetFungaEventsAsync(new(){PageSize=10,HumidityRange=new(){Min=50,Max=80},ContinuationToken=first.ContinuationToken});Check(first.Items.Concat(second.Items).Select(x=>x.EventId).Distinct().Count()==6&&!second.HasMore,"page size changes do not lose heads");});
Add("edge tampered fingerprint rejected before I/O",async()=> {var f=Fixture("canonical");var first=await f.Service.GetFungaEventsAsync(new(){PageSize=1});var raw=first.ContinuationToken!.Replace('-','+').Replace('_','/');var env=JObject.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw.PadRight((raw.Length+3)/4*4,'='))));env["Fingerprint"]="tampered-fixture";var bad=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(env.ToString())).TrimEnd('=').Replace('+','-').Replace('/','_');int reads=f.Container.Reads;Check((await f.Controller.GetFungaEvents(continuationToken:bad)).Result is BadRequestObjectResult&&f.Container.Reads==reads,"tampered filter binding400");});
Add("edge empty page budget503",async()=> {var f=Fixture("canonical");f.Container.EmptyPages=int.MaxValue;Check((await f.Controller.GetFungaEvents(pageSize:1)).Result is ObjectResult{StatusCode:503},"bounded empty page loop503");Check(f.Container.Reads==38,"explicit read budget3*(1+1)+32");});
Add("edge oversized provider page503",async()=> {var f=Fixture("canonical");f.Container.OversizePage=true;Check((await f.Controller.GetFungaEvents()).Result is ObjectResult{StatusCode:503},"never skip extra provider rows");});
Add("edge direct page size rejected",async()=> {var f=Fixture("canonical");foreach(var size in new[]{0,1001})await RejectArgument(()=>f.Service.GetFungaEventsAsync(new(){PageSize=size}));Check(f.Container.Reads==0,"invalid direct page size no reads");});
Add("edge parameter injection stays data",async()=> {var f=Fixture("core");const string input="fixture' OR true";var r=await f.Service.GetFungaEventsAsync(new(){SourceDevice=input});Check(!r.Items.Any(),"no matching source");Check(f.Container.Queries.All(q=>!q.QueryText.Contains(input)&&Equals(q.Parameters["@sourceDevice"],input)),"all three streams parameterize input");});
Add("edge one-sided maximum and explicit finite bounds",async()=> {var f=Fixture("ingestion");Ids(Ok(await f.Controller.GetFungaEvents(humidityMax:50,pHMax:7)),"ingestion-zero","ingestion-middle");var r=await f.Service.GetFungaEventsAsync(new(){pHRange=new(){Min=-1,Max=15}});Check(r.Items.Count()==4,"explicit finite bounds are not clamped to new policy");});
Add("edge mycorrhizal annotation paths",async()=> {foreach(var shape in new[]{"canonical","core","ingestion"}){var f=Fixture(shape);string decoded=shape=="canonical"?"decoded_meaning":shape=="core"?"decodedMeaning":"DecodedMeaning";string annotations=shape=="ingestion"?"Annotations":"annotations";f.Container.Rows[1][decoded]=new JObject{[annotations]=new JObject{["mycorrhizal_type"]="fixture"}};Ids(await f.Service.GetFungaEventsAsync(new(){MycorrhizalOnly=true}),shape+"-middle");}});
Add("edge arbitrary payload keys preserved",async()=> {var f=Fixture("core");f.Container.Rows[1]["signalVector"]=new JObject{["Snake_Key"]=new JObject{["pH"]=5}};var r=await f.Service.GetFungaEventsAsync(new(){HumidityRange=new(){Min=50,Max=50}});var json=(System.Text.Json.JsonElement)r.Items.Single().SignalVector!;Check(json.GetProperty("Snake_Key").GetProperty("pH").GetInt32()==5,"payload keys must not be normalized");});

Add("edge raw ISO string ordering across formats",async()=> {var f=Fixture("canonical","core");var whole=(JObject)f.Container.Rows[1].DeepClone();whole["timestamp"]="2020-01-02T00:00:00Z";var fraction=(JObject)f.Container.Rows[6].DeepClone();fraction["timestamp"]="2020-01-02T00:00:00.1Z";f.Container.Rows.Clear();f.Container.Rows.AddRange(new[]{whole,fraction});var r=await f.Service.GetFungaEventsAsync(new(){SortOrder="timestamp_asc"});Check(r.Items.Select(x=>x.EventId).SequenceEqual(new[]{"core-middle","canonical-middle"}),"match provider ISO string ordering, not normalized DateTime order");});
Add("edge actual Newtonsoft reader preserves raw timestamps",async()=> {await Task.CompletedTask;const string raw="{\"timestamp\":\"2020-01-02T00:00:00.1000000Z\",\"kingdomDomain\":\"FUNGA.fixture\"}";var doc=JsonNet.DeserializeObject<FungaEventReader.RawDocument>(raw)!;Check(doc.Document["timestamp"]!.Type==JTokenType.String&&doc.Document["timestamp"]!.Value<string>()=="2020-01-02T00:00:00.1000000Z","SDK-compatible converter retains exact raw SQL sort key");});

var failures=0;var receipts=new List<object>();
foreach(var(name,run)in tests){try{await run();Console.WriteLine("PASS: "+name);receipts.Add(new{name,passed=true});}catch(Exception e){failures++;Console.WriteLine("FAIL: "+name+": "+e.Message);receipts.Add(new{name,passed=false,error=e.Message});}}
Console.WriteLine($"RESULT: {tests.Count-failures} passed, {failures} failed; {tests.Count} total; no live Cosmos or HTTP calls");
var path=Environment.GetEnvironmentVariable("BATCH8_RECEIPT");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,Json.Serialize(receipts,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
return failures==0?0:1;

static void Check(bool value,string message){if(!value)throw new Exception(message);}
static void Ids(PagedResult<MycorrhizaeEvent> result,params string[] expected)=>Check(result.Items.Select(x=>x.EventId).Order().SequenceEqual(expected.Order()),"expected ["+string.Join(',',expected)+"] got ["+string.Join(',',result.Items.Select(x=>x.EventId))+"]");
static PagedResult<MycorrhizaeEvent> Ok(ActionResult<PagedResult<MycorrhizaeEvent>> result)=>result.Result is OkObjectResult{Value:PagedResult<MycorrhizaeEvent> value}?value:throw new Exception("Expected200");
static async Task RejectArgument(Func<Task<PagedResult<MycorrhizaeEvent>>> action){try{await action();}catch(ArgumentException){return;}throw new Exception("Expected argument rejection");}
static (FungaService Service,FungaController Controller,Container Container) Fixture(params string[] shapes){
 var rows=new List<JObject>();
 foreach(var shape in shapes)foreach(var (id,h,p,temp,day) in new (string,double?,double?,double,int)[]{("zero",0,0,20,1),("middle",50,7,21,2),("wet",80,6,22,3),("high",100,14,23,4),("unknown",null,null,24,5)}) {
  var model=new MycorrhizaeEvent{EventId=shape+"-"+id,SourceDevice="fixture-device",KingdomDomain="FUNGA.fixture",Timestamp=new(2020,1,day,0,0,0,DateTimeKind.Utc),References=new(){Taxonomy=new(){Phylum="fixture-phylum"},Environment=new(){Humidity=h,pH=p,Temperature=temp,Substrate="fixture-substrate"}}};
  string json=shape=="canonical"?Json.Serialize(model):JsonNet.SerializeObject(model,new Newtonsoft.Json.JsonSerializerSettings{ContractResolver=shape=="core"?new CamelCasePropertyNamesContractResolver():new DefaultContractResolver()});using var reader=new Newtonsoft.Json.JsonTextReader(new StringReader(json)){DateParseHandling=Newtonsoft.Json.DateParseHandling.None};rows.Add(JObject.Load(reader));
 }
 var container=new Container(rows);var service=new FungaService(new CosmosClient(container),NullLogger<FungaService>.Instance,new HttpClient(new RejectHttp()));return(service,new FungaController(service,NullLogger<FungaController>.Instance),container);
}
sealed class RejectHttp:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>throw new Exception("Offline fixture rejects HTTP");}
