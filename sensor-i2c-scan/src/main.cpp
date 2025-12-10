#include <Arduino.h>
#include <Wire.h>

void setup() {
  Serial.begin(115200);
  delay(500);
  Serial.println("I2C scanner starting (SDA=21, SCL=22)...");
  Wire.begin(21, 22);
}

void loop() {
  byte error, address;
  int nDevices = 0;
  Serial.println("Scanning...");
  for (address = 1; address < 127; address++) {
    Wire.beginTransmission(address);
    error = Wire.endTransmission();
    if (error == 0) {
      Serial.print("I2C device found at 0x");
      if (address < 16) Serial.print('0');
      Serial.print(address, HEX);
      Serial.println(" !");
      nDevices++;
    }
  }
  if (nDevices == 0) Serial.println("No I2C devices found\n");
  else Serial.println("Done.\n");
  delay(3000);
}

