#include <Arduino.h>
#include <HardwareSerial.h>
#include <TinyGsmClient.h>
#include <ArduinoJson.h>

#ifndef MODEM_PWRKEY_PIN
#define MODEM_PWRKEY_PIN 4
#endif
#ifndef MODEM_RX_PIN
#define MODEM_RX_PIN 26
#endif
#ifndef MODEM_TX_PIN
#define MODEM_TX_PIN 27
#endif
#ifndef SENSOR_RX_PIN
#define SENSOR_RX_PIN 16
#endif
#ifndef SENSOR_TX_PIN
#define SENSOR_TX_PIN 17
#endif
#ifndef APN_NAME
#define APN_NAME "iot.1nce.net"
#endif
#ifndef UDP_HOST
#define UDP_HOST "udp.os.1nce.com"
#endif
#ifndef UDP_PORT
#define UDP_PORT 4445
#endif

HardwareSerial SerialMon(0);
HardwareSerial SerialAT(1);
HardwareSerial SerialData(2);

TinyGsm modem(SerialAT);

static String deviceImei;
static const uint32_t SENSOR_BAUD = 115200;
static const uint32_t MODEM_BAUD  = 115200;
static const size_t   MAX_UDP_SAFE = 508;

static void pressPwrKey() {
  pinMode(MODEM_PWRKEY_PIN, OUTPUT);
  digitalWrite(MODEM_PWRKEY_PIN, LOW);
  delay(100);
  digitalWrite(MODEM_PWRKEY_PIN, HIGH);
  delay(1200);
  digitalWrite(MODEM_PWRKEY_PIN, LOW);
  delay(2000);
}

static bool tryAttachWithMode(uint8_t cmnbMode) {
  SerialMon.print(F("[NET] Setting RAT CMNB=")); SerialMon.println(cmnbMode);
  modem.sendAT("+CNMP=38"); modem.waitResponse(5000); // LTE only
  modem.sendAT("+CMNB=", cmnbMode); modem.waitResponse(5000); // 1=LTE-M, 2=NB-IoT
  SerialMon.println(F("[NET] Waiting for network..."));
  if (!modem.waitForNetwork(90000)) {
    SerialMon.println(F("[NET] waitForNetwork failed"));
    return false;
  }
  SerialMon.println(F("[NET] Network ready, connecting GPRS..."));
  if (!modem.gprsConnect(APN_NAME)) {
    SerialMon.println(F("[NET] gprsConnect failed"));
    return false;
  }
  bool ok = modem.isGprsConnected();
  SerialMon.print(F("[NET] GPRS connected = ")); SerialMon.println(ok ? F("true") : F("false"));
  return ok;
}

static bool attachNetwork() {
  // Try LTE-M first, then NB-IoT fallback
  if (tryAttachWithMode(1)) return true;
  SerialMon.println(F("[NET] Falling back to NB-IoT (CMNB=2)"));
  return tryAttachWithMode(2);
}

static bool sendUdp(const String &payload) {
  if (!modem.isGprsConnected()) {
    if (!attachNetwork()) return false;
  }

  // Clean socket state
  SerialMon.print(F("[UDP] Opening socket to ")); SerialMon.print(F(UDP_HOST)); SerialMon.print(F(":")); SerialMon.println(UDP_PORT);
  modem.sendAT("+CIPSHUT"); modem.waitResponse(8000);
  modem.sendAT("+CIPMUX=0"); modem.waitResponse(2000);

  // Open UDP socket id 0
  modem.sendAT("+CIPOPEN=0,\"UDP\",\"", UDP_HOST, "\",", UDP_PORT);
  if (modem.waitResponse(15000, "OK") != 1) {
    // Some firmware uses only +CIPOPEN URC; still wait briefly
    if (modem.waitResponse(5000, "+CIPOPEN:") != 1) {
      SerialMon.println(F("[UDP] CIPOPEN failed"));
      return false;
    }
  }

  // Send payload
  modem.sendAT("+CIPSEND=0,", payload.length());
  modem.stream.write((const uint8_t*)payload.c_str(), payload.length());
  modem.stream.write((char)0x1A);
  if (modem.waitResponse(10000, "SEND OK") != 1) {
    SerialMon.println(F("[UDP] SEND failed"));
    modem.sendAT("+CIPCLOSE=0"); modem.waitResponse(3000);
    return false;
  }

  SerialMon.println(F("[UDP] SEND OK"));
  modem.sendAT("+CIPCLOSE=0"); modem.waitResponse(3000);
  return true;
}

static String makeJson(const String &dataLine) {
  StaticJsonDocument<256> doc;
  doc["imei"] = deviceImei;
  doc["ts"]   = (uint64_t)millis();
  doc["data"] = dataLine;
  String out; serializeJson(doc, out);
  if (out.length() > MAX_UDP_SAFE) out.remove(MAX_UDP_SAFE - 1);
  return out;
}

void setup() {
  SerialMon.begin(115200);
  delay(200);

  SerialAT.begin(MODEM_BAUD, SERIAL_8N1, MODEM_RX_PIN, MODEM_TX_PIN);
  SerialData.begin(SENSOR_BAUD, SERIAL_8N1, SENSOR_RX_PIN, SENSOR_TX_PIN);

  pressPwrKey();
  delay(2000);

  for (int i = 0; i < 3; i++) {
    if (modem.restart()) break;
    delay(2000);
  }

  deviceImei = modem.getIMEI();
  if (deviceImei.length() == 0) deviceImei = "unknown";

  attachNetwork();

  String hello = makeJson(F("hello from T-SIM7000G"));
  SerialMon.print(F("[HELLO] ")); SerialMon.println(hello);
  bool helloOk = sendUdp(hello);
  SerialMon.print(F("[HELLO] sent=")); SerialMon.println(helloOk ? F("true") : F("false"));
}

void loop() {
  static String buf;
  while (SerialData.available()) {
    char c = (char)SerialData.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (!buf.isEmpty()) {
        String json = makeJson(buf);
        SerialMon.print(F("[LINE] ")); SerialMon.println(json);
        bool ok = sendUdp(json);
        SerialMon.print(F("[LINE] sent=")); SerialMon.println(ok ? F("true") : F("false"));
        buf = "";
      }
    } else {
      if (buf.length() < (MAX_UDP_SAFE - 64)) buf += c;
    }
  }

  static uint32_t lastCheck = 0;
  if (millis() - lastCheck > 30000) {
    lastCheck = millis();
    if (!modem.isGprsConnected()) attachNetwork();
  }
}

