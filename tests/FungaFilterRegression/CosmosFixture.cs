using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

// A strict, small SQL contract seam, not a Cosmos engine/emulator or SDK substitute
// for production. The harness links actual service/controller source against it.
namespace Microsoft.Azure.Cosmos;

public sealed class CosmosClient(Container container) { public Database GetDatabase(string name) => name=="mindex" ? new(container) : throw new Exception("Unexpected database"); }
public sealed class Database(Container container) { public Container GetContainer(string name) => name is "events" or "taxonomy" ? container : throw new Exception("Unexpected container"); }
public sealed class QueryRequestOptions { public int? MaxItemCount {get;set;} }
public sealed class QueryDefinition(string text) {
    public string QueryText {get;}=text;
    public Dictionary<string,object> Parameters {get;}=new();
    public QueryDefinition WithParameter(string name,object value) { Parameters.Add(name,value);return this; }
}
public sealed class Container(IEnumerable<JObject> rows) {
    public List<JObject> Rows {get;}=rows.Select(x=>(JObject)x.DeepClone()).ToList();
    public List<QueryDefinition> Queries {get;}=new();
    public int Reads {get;private set;}
    public int EmptyPages {get;set;}
    public bool FailRead {get;set;}
    public bool OversizePage {get;set;}
    public CancellationToken LastCancellation {get;private set;}
    public FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition query,string? continuationToken=null,QueryRequestOptions? requestOptions=null) {
        Queries.Add(query);
        return new FeedIterator<T>(async token=> {
            await Task.CompletedTask;Reads++;LastCancellation=token;token.ThrowIfCancellationRequested();
            if(FailRead)throw new IOException("Deterministic provider fixture failure");
            var signature=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(query.QueryText))).Substring(0,12);
            int start=0;
            if(continuationToken!=null) {
                var parts=continuationToken.Split(':');
                if(parts.Length!=3||parts[0]!="fixture"||parts[1]!=signature||!int.TryParse(parts[2],out start))throw new Exception("Invalid fixture provider cursor");
            }
            if(EmptyPages-->0)return new FeedResponse<T>([], $"fixture:{signature}:{start}");
            var order=Regex.Match(query.QueryText,@" ORDER BY c\.([\w.]+) (ASC|DESC)$");
            if(!order.Success)throw new Exception("Fixture requires one real document sort path");
            var where=query.QueryText[..order.Index].Replace("SELECT * FROM c WHERE ","");
            var matches=Rows.Where(x=>SqlPredicate.Evaluate(where,query.Parameters,x)).ToList();
            Func<JObject,string> key=x=>Value(x,order.Groups[1].Value)?.ToString(Formatting.None)??"";
            matches=(order.Groups[2].Value=="ASC"?matches.OrderBy(key,StringComparer.Ordinal):matches.OrderByDescending(key,StringComparer.Ordinal)).ToList();
            var take=OversizePage?2:requestOptions?.MaxItemCount??50;
            var selected=matches.Skip(start).Take(take).Select(x=>typeof(T)==typeof(JObject)?(T)(object)x.DeepClone():x.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}))!).ToList();
            var next=start+selected.Count;
            return new FeedResponse<T>(selected,next<matches.Count?$"fixture:{signature}:{next}":null);
        });
    }
    internal static JToken? Value(JObject row,string path) {
        JToken? value=row;
        foreach(var part in path.Split('.')) { if(value is not JObject obj||!obj.TryGetValue(part,StringComparison.Ordinal,out value))return null; }
        return value;
    }
}
public sealed class FeedIterator<T>(Func<CancellationToken,Task<FeedResponse<T>>> read) : IDisposable {
    public void Dispose() {}
    public bool HasMoreResults {get;private set;}=true;
    public async Task<FeedResponse<T>> ReadNextAsync(CancellationToken token=default) { HasMoreResults=false;return await read(token); }
}
public sealed class FeedResponse<T>(IEnumerable<T> values,string? continuation):List<T>(values) { public string? ContinuationToken {get;}=continuation; }

internal sealed class SqlPredicate {
    readonly List<string> tokens;readonly Dictionary<string,object> parameters;readonly JObject row;int i;
    SqlPredicate(string sql,Dictionary<string,object> p,JObject r) {
        tokens=Regex.Matches(sql,@"c\.[\w.]+|@[\w]+|'[^']*'|>=|<=|=|\(|\)|[A-Z_]+").Select(m=>m.Value).ToList();parameters=p;row=r;
        var consumed=string.Concat(tokens);if(consumed!=Regex.Replace(sql,@"\s+",""))throw new Exception("Unsupported SQL fixture syntax: "+sql);
    }
    public static bool Evaluate(string sql,Dictionary<string,object> p,JObject r) {var x=new SqlPredicate(sql,p,r);var result=x.Or();if(x.i!=x.tokens.Count)throw new Exception("Unconsumed SQL fixture tokens");return result;}
    bool Match(string value) {if(i<tokens.Count&&tokens[i]==value){i++;return true;}return false;}
    string Next()=>i<tokens.Count?tokens[i++]:throw new Exception("Incomplete SQL fixture");
    void Require(string value) {if(!Match(value))throw new Exception("Expected "+value);}
    bool Or(){var a=And();while(Match("OR")){var b=And();a=a||b;}return a;}
    bool And(){var a=Term();while(Match("AND")){var b=Term();a=a&&b;}return a;}
    bool Term() {
        if(Match("NOT"))return !Term();
        if(Match("(")){var result=Or();Require(")");return result;}
        var left=Next();
        if(left is "IS_DEFINED" or "IS_NULL" or "IS_NUMBER") {
            Require("(");var p=Next();Require(")");var v=Container.Value(row,p[2..]);
            return left switch {"IS_DEFINED"=>v!=null,"IS_NULL"=>v?.Type==JTokenType.Null,_=>v?.Type is JTokenType.Integer or JTokenType.Float};
        }
        if(!left.StartsWith("c."))throw new Exception("Expected fixture document path");
        var value=Container.Value(row,left[2..]);var op=Next();var right=Next();
        if(op=="LIKE")return value?.Type==JTokenType.String&&right=="'FUNGA%'"&&value.Value<string>()!.StartsWith("FUNGA",StringComparison.Ordinal);
        object expected=right.StartsWith('@')?parameters[right]:right.Trim('\'');
        if(value==null||value.Type==JTokenType.Null)return false;
        int comparison;
        if(expected is double number) {
            if(value.Type is not (JTokenType.Integer or JTokenType.Float))return false;
            comparison=value.Value<double>().CompareTo(number);
        } else if(expected is DateTime time)comparison=StringComparer.Ordinal.Compare(value.Value<string>(),JsonConvert.SerializeObject(time).Trim('"'));
        else comparison=StringComparer.Ordinal.Compare(value.Value<string>(),Convert.ToString(expected,CultureInfo.InvariantCulture));
        return op switch {"="=>comparison==0,">="=>comparison>=0,"<="=>comparison<=0,_=>throw new Exception("Unsupported fixture operator")};
    }
}
