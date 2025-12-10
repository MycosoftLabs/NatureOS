#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_BME680.h>

#ifndef SENSOR_UART_TX
#define SENSOR_UART_TX 17
#endif
#ifndef SENSOR_UART_RX
#define SENSOR_UART_RX 16
#endif

HardwareSerial SerialMon(0);
HardwareSerial SerialOut(2);

Adafruit_BME680 bme; // I2C

static const uint32_t PUBLISH_PERIOD_MS = 5000;
static const uint32_t STARTUP_DELAY_MS  = 2000;

void setup() {
  SerialMon.begin(115200);
  delay(STARTUP_DELAY_MS);

  SerialOut.begin(115200, SERIAL_8N1, SENSOR_UART_RX, SENSOR_UART_TX);

  Wire.begin();
  Wire.setClock(100000); // 100 kHz for stability on longer jumpers
  // Force address 0x77 as detected by I2C scanner
  if (!bme.begin(0x77)) {
    SerialMon.println(F("BME680 not found at 0x77"));
  } else {
    SerialMon.println(F("BME680 detected at 0x77"));
  }

  bme.setTemperatureOversampling(BME680_OS_8X);
  bme.setHumidityOversampling(BME680_OS_2X);
  bme.setPressureOversampling(BME680_OS_4X);
  bme.setIIRFilterSize(BME680_FILTER_SIZE_3);
  // Start without gas heater; add later when basics are confirmed
  bme.setGasHeater(0, 0);
}

void loop() {
  // Wait for a valid reading up to 2 seconds
  bool ok = false;
  uint32_t start = millis();
  while (millis() - start < 2000) {
    if (bme.performReading()) { ok = true; break; }
    delay(50);
  }

  if (!ok) {
    SerialMon.println(F("BME680 read failed"));
    delay(PUBLISH_PERIOD_MS);
    return;
  }

  String line;
  line.reserve(160);
  line += '{';
  line += "\"src\":\"bme680\",";
  line += "\"t\":"; line += String(bme.temperature, 2); line += ',';
  line += "\"h\":"; line += String(bme.humidity, 2);    line += ',';
  line += "\"p\":"; line += String(bme.pressure * 100.0, 0);
  line += '}';
  SerialOut.println(line);
  SerialMon.println(line);

  delay(PUBLISH_PERIOD_MS);
}

