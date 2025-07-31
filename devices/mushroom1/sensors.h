/*
 * Sensor Management Library for Mushroom 1
 * Handles ADS1299 bioelectric acquisition and BME688 environmental sensing
 */

#ifndef SENSORS_H
#define SENSORS_H

#include <Arduino.h>
#include <SPI.h>
#include <Wire.h>
#include <Adafruit_BME680.h>
#include "config.h"

// ADS1299 Register Definitions
#define ADS1299_ID        0x00
#define ADS1299_CONFIG1   0x01
#define ADS1299_CONFIG2   0x02
#define ADS1299_CONFIG3   0x03
#define ADS1299_LOFF      0x04
#define ADS1299_CH1SET    0x05
#define ADS1299_CH2SET    0x06
#define ADS1299_CH3SET    0x07
#define ADS1299_CH4SET    0x08
#define ADS1299_CH5SET    0x09
#define ADS1299_CH6SET    0x0A
#define ADS1299_CH7SET    0x0B
#define ADS1299_CH8SET    0x0C
#define ADS1299_BIAS_SENSP 0x0D
#define ADS1299_BIAS_SENSN 0x0E
#define ADS1299_LOFF_SENSP 0x0F
#define ADS1299_LOFF_SENSN 0x10
#define ADS1299_LOFF_FLIP  0x11
#define ADS1299_LOFF_STATP 0x12
#define ADS1299_LOFF_STATN 0x13
#define ADS1299_GPIO       0x14
#define ADS1299_MISC1      0x15
#define ADS1299_MISC2      0x16
#define ADS1299_CONFIG4    0x17

// ADS1299 Commands
#define ADS1299_WAKEUP    0x02
#define ADS1299_STANDBY   0x04
#define ADS1299_RESET     0x06
#define ADS1299_START     0x08
#define ADS1299_STOP      0x0A
#define ADS1299_RDATAC    0x10
#define ADS1299_SDATAC    0x11
#define ADS1299_RDATA     0x12
#define ADS1299_RREG      0x20
#define ADS1299_WREG      0x40

// Channel settings
#define ADS1299_CHANNEL_OFF   0x80
#define ADS1299_CHANNEL_ON    0x00
#define ADS1299_GAIN_1        0x00
#define ADS1299_GAIN_2        0x10
#define ADS1299_GAIN_4        0x20
#define ADS1299_GAIN_6        0x30
#define ADS1299_GAIN_8        0x40
#define ADS1299_GAIN_12       0x50
#define ADS1299_GAIN_24       0x60

// Data types
struct EnvironmentalData {
  float temperature;
  float humidity;
  float pressure;
  float gasResistance;
  bool valid;
  unsigned long timestamp;
};

struct BioelectricData {
  int32_t channels[8];
  float voltages[8];
  bool leadOff[8];
  int signalQuality;
  bool valid;
  unsigned long timestamp;
};

struct FilterState {
  float highpass[8][2];  // Previous input and output for highpass
  float lowpass[8][2];   // Previous input and output for lowpass
  float notch[8][4];     // Notch filter state variables
};

class SensorManager {
private:
  Adafruit_BME680 bme688;
  bool ads1299Available;
  bool bme688Available;
  
  // Filter state
  FilterState filterState;
  bool filtersInitialized;
  
  // Calibration data
  float channelOffsets[8];
  float channelGains[8];
  
  // Signal quality assessment
  float noiseFloor[8];
  float signalPower[8];
  
  // Private methods
  void initializeADS1299();
  void initializeBME688();
  void initializeFilters();
  void loadCalibrationData();
  
  // ADS1299 low-level functions
  void writeRegister(uint8_t reg, uint8_t value);
  uint8_t readRegister(uint8_t reg);
  void sendCommand(uint8_t command);
  int32_t convertToSigned(uint32_t unsignedValue);
  
  // Signal processing
  void applyFilters(float* samples);
  float highpassFilter(float input, int channel);
  float lowpassFilter(float input, int channel);
  float notchFilter(float input, int channel);
  void updateSignalQuality(float* samples);
  
  // Validation
  bool validateBioelectricData(int32_t* rawData);
  bool validateEnvironmentalData(float temp, float hum, float pres);

public:
  SensorManager();
  ~SensorManager();
  
  // Initialization
  bool begin();
  void reset();
  void calibrate();
  
  // Data acquisition
  bool readBioelectricChannels(float* voltages);
  bool readBioelectricChannelsRaw(int32_t* rawValues);
  bool readEnvironmentalData(float& temperature, float& humidity, float& pressure, float& gasResistance);
  
  // Individual sensor access
  bool readADS1299(BioelectricData& data);
  bool readBME688(EnvironmentalData& data);
  
  // Utility functions
  float readBatteryVoltage();
  int calculateSignalQuality(float* channels);
  bool performSelfTest();
  void setGain(int channel, uint8_t gain);
  void enableChannel(int channel, bool enabled);
  
  // Configuration
  void setSamplingRate(int rate);
  void enableFilters(bool highpass, bool lowpass, bool notch);
  void setFilterCutoffs(float highpassHz, float lowpassHz);
  
  // Status and diagnostics
  bool isADS1299Available() { return ads1299Available; }
  bool isBME688Available() { return bme688Available; }
  void printStatus();
  void printCalibrationData();
  
  // Data export
  String getCSVHeader();
  String formatDataAsCSV(const BioelectricData& bio, const EnvironmentalData& env);
};

// Implementation begins here
SensorManager::SensorManager() {
  ads1299Available = false;
  bme688Available = false;
  filtersInitialized = false;
  
  // Initialize calibration data from config
  channelOffsets[0] = CALIBRATION_OFFSET_CH0;
  channelOffsets[1] = CALIBRATION_OFFSET_CH1;
  channelOffsets[2] = CALIBRATION_OFFSET_CH2;
  channelOffsets[3] = CALIBRATION_OFFSET_CH3;
  channelOffsets[4] = CALIBRATION_OFFSET_CH4;
  channelOffsets[5] = CALIBRATION_OFFSET_CH5;
  channelOffsets[6] = CALIBRATION_OFFSET_CH6;
  channelOffsets[7] = CALIBRATION_OFFSET_CH7;
  
  channelGains[0] = CALIBRATION_GAIN_CH0;
  channelGains[1] = CALIBRATION_GAIN_CH1;
  channelGains[2] = CALIBRATION_GAIN_CH2;
  channelGains[3] = CALIBRATION_GAIN_CH3;
  channelGains[4] = CALIBRATION_GAIN_CH4;
  channelGains[5] = CALIBRATION_GAIN_CH5;
  channelGains[6] = CALIBRATION_GAIN_CH6;
  channelGains[7] = CALIBRATION_GAIN_CH7;
}

SensorManager::~SensorManager() {
  if (ads1299Available) {
    sendCommand(ADS1299_STOP);
    sendCommand(ADS1299_STANDBY);
  }
}

bool SensorManager::begin() {
  Serial.println("🔬 Initializing sensors...");
  
  // Initialize SPI for ADS1299
  SPI.begin();
  SPI.setDataMode(SPI_MODE1);
  SPI.setClockDivider(SPI_CLOCK_DIV8); // 2 MHz
  SPI.setBitOrder(MSBFIRST);
  
  // Initialize I2C for BME688
  Wire.begin(BME688_SDA_PIN, BME688_SCL_PIN);
  
  // Initialize sensors
  initializeADS1299();
  initializeBME688();
  initializeFilters();
  
  if (ads1299Available || bme688Available) {
    Serial.println("✅ Sensor initialization completed");
    return true;
  } else {
    Serial.println("❌ No sensors available");
    return false;
  }
}

void SensorManager::initializeADS1299() {
  pinMode(ADS1299_CS_PIN, OUTPUT);
  pinMode(ADS1299_DRDY_PIN, INPUT);
  digitalWrite(ADS1299_CS_PIN, HIGH);
  
  delay(100);
  
  // Reset ADS1299
  sendCommand(ADS1299_RESET);
  delay(100);
  
  // Check device ID
  uint8_t id = readRegister(ADS1299_ID);
  Serial.println("ADS1299 ID: 0x" + String(id, HEX));
  
  if ((id & 0x1F) == 0x1E) { // ADS1299 family
    ads1299Available = true;
    Serial.println("✅ ADS1299 detected");
    
    // Configure ADS1299
    writeRegister(ADS1299_CONFIG1, 0x96); // 250 SPS, continuous conversion
    writeRegister(ADS1299_CONFIG2, 0xD0); // Generate test signal
    writeRegister(ADS1299_CONFIG3, 0xEC); // Enable internal reference
    
    // Configure channels
    for (int i = 0; i < 8; i++) {
      writeRegister(ADS1299_CH1SET + i, ADS1299_CHANNEL_ON | ADS1299_GAIN_24);
    }
    
    // Start conversion
    sendCommand(ADS1299_START);
    sendCommand(ADS1299_RDATAC);
    
  } else {
    ads1299Available = false;
    Serial.println("❌ ADS1299 not detected");
  }
}

void SensorManager::initializeBME688() {
  if (bme688.begin(BME688_I2C_ADDRESS)) {
    bme688Available = true;
    Serial.println("✅ BME688 detected");
    
    // Configure BME688
    bme688.setTemperatureOversampling(BME680_OS_8X);
    bme688.setHumidityOversampling(BME680_OS_2X);
    bme688.setPressureOversampling(BME680_OS_4X);
    bme688.setIIRFilterSize(BME680_FILTER_SIZE_3);
    bme688.setGasHeater(320, 150); // 320°C for 150 ms
    
  } else {
    bme688Available = false;
    Serial.println("❌ BME688 not detected");
  }
}

void SensorManager::initializeFilters() {
  // Clear filter state
  memset(&filterState, 0, sizeof(FilterState));
  filtersInitialized = true;
  Serial.println("✅ Digital filters initialized");
}

bool SensorManager::readBioelectricChannels(float* voltages) {
  if (!ads1299Available) return false;
  
  // Wait for data ready
  if (digitalRead(ADS1299_DRDY_PIN) == HIGH) {
    return false; // No new data
  }
  
  digitalWrite(ADS1299_CS_PIN, LOW);
  
  // Read status (3 bytes) + 8 channels (3 bytes each) = 27 bytes total
  uint8_t buffer[27];
  for (int i = 0; i < 27; i++) {
    buffer[i] = SPI.transfer(0x00);
  }
  
  digitalWrite(ADS1299_CS_PIN, HIGH);
  
  // Convert raw data to voltages
  for (int i = 0; i < 8; i++) {
    int32_t rawValue = 0;
    rawValue = ((uint32_t)buffer[3 + i*3] << 16) | 
               ((uint32_t)buffer[4 + i*3] << 8) | 
               ((uint32_t)buffer[5 + i*3]);
    
    // Convert to signed 24-bit
    if (rawValue & 0x800000) {
      rawValue |= 0xFF000000;
    }
    
    // Convert to voltage
    float voltage = (float)rawValue * ADC_REFERENCE_VOLTAGE / (8388608.0 * ADC_GAIN_SETTING);
    
    // Apply calibration
    voltage = (voltage - channelOffsets[i]) * channelGains[i];
    
    voltages[i] = voltage;
  }
  
  // Apply filters if enabled
  if (SIGNAL_FILTER_ENABLED && filtersInitialized) {
    applyFilters(voltages);
  }
  
  // Update signal quality
  updateSignalQuality(voltages);
  
  return true;
}

bool SensorManager::readEnvironmentalData(float& temperature, float& humidity, float& pressure, float& gasResistance) {
  if (!bme688Available) return false;
  
  if (!bme688.performReading()) {
    return false;
  }
  
  temperature = bme688.temperature + BME688_TEMP_OFFSET;
  humidity = bme688.humidity + BME688_HUMIDITY_OFFSET;
  pressure = bme688.pressure / 100.0; // Convert Pa to hPa
  gasResistance = bme688.gas_resistance / 1000.0; // Convert to kOhms
  
  return validateEnvironmentalData(temperature, humidity, pressure);
}

float SensorManager::readBatteryVoltage() {
  int adcValue = analogRead(BATTERY_ADC_PIN);
  float voltage = (adcValue / 4095.0) * 3.3 * BATTERY_VOLTAGE_DIVIDER;
  return voltage;
}

int SensorManager::calculateSignalQuality(float* channels) {
  float totalNoise = 0;
  float totalSignal = 0;
  
  for (int i = 0; i < 8; i++) {
    float signal = fabs(channels[i]);
    totalSignal += signal;
    
    // Simple noise estimation (high frequency content)
    if (i > 0) {
      float diff = fabs(channels[i] - channels[i-1]);
      totalNoise += diff;
    }
  }
  
  if (totalNoise > 0) {
    float snr = totalSignal / totalNoise;
    int quality = constrain((int)(snr * 10), 0, 100);
    return quality;
  }
  
  return 50; // Default moderate quality
}

void SensorManager::writeRegister(uint8_t reg, uint8_t value) {
  digitalWrite(ADS1299_CS_PIN, LOW);
  SPI.transfer(ADS1299_WREG | reg);
  SPI.transfer(0x00); // Number of registers - 1
  SPI.transfer(value);
  digitalWrite(ADS1299_CS_PIN, HIGH);
  delayMicroseconds(5);
}

uint8_t SensorManager::readRegister(uint8_t reg) {
  digitalWrite(ADS1299_CS_PIN, LOW);
  SPI.transfer(ADS1299_RREG | reg);
  SPI.transfer(0x00); // Number of registers - 1
  uint8_t value = SPI.transfer(0x00);
  digitalWrite(ADS1299_CS_PIN, HIGH);
  return value;
}

void SensorManager::sendCommand(uint8_t command) {
  digitalWrite(ADS1299_CS_PIN, LOW);
  SPI.transfer(command);
  digitalWrite(ADS1299_CS_PIN, HIGH);
  delayMicroseconds(5);
}

void SensorManager::applyFilters(float* samples) {
  for (int i = 0; i < 8; i++) {
    if (SIGNAL_FILTER_ENABLED) {
      samples[i] = highpassFilter(samples[i], i);
      samples[i] = lowpassFilter(samples[i], i);
      if (NOTCH_FILTER_60HZ) {
        samples[i] = notchFilter(samples[i], i);
      }
    }
  }
}

float SensorManager::highpassFilter(float input, int channel) {
  // Simple first-order highpass filter
  float alpha = 0.95; // Cutoff frequency dependent
  float output = alpha * (filterState.highpass[channel][1] + input - filterState.highpass[channel][0]);
  
  filterState.highpass[channel][0] = input;
  filterState.highpass[channel][1] = output;
  
  return output;
}

float SensorManager::lowpassFilter(float input, int channel) {
  // Simple first-order lowpass filter
  float alpha = 0.1; // Cutoff frequency dependent
  float output = alpha * input + (1 - alpha) * filterState.lowpass[channel][1];
  
  filterState.lowpass[channel][0] = input;
  filterState.lowpass[channel][1] = output;
  
  return output;
}

void SensorManager::updateSignalQuality(float* samples) {
  // Update noise floor and signal power estimates
  for (int i = 0; i < 8; i++) {
    float power = samples[i] * samples[i];
    signalPower[i] = 0.9 * signalPower[i] + 0.1 * power;
  }
}

bool SensorManager::validateEnvironmentalData(float temp, float hum, float pres) {
  return (temp > -40 && temp < 85 && 
          hum >= 0 && hum <= 100 && 
          pres > 300 && pres < 1100);
}

void SensorManager::printStatus() {
  Serial.println("\n🔬 Sensor Status:");
  Serial.println("ADS1299: " + String(ads1299Available ? "✅ Available" : "❌ Not Available"));
  Serial.println("BME688: " + String(bme688Available ? "✅ Available" : "❌ Not Available"));
  Serial.println("Filters: " + String(filtersInitialized ? "✅ Initialized" : "❌ Not Initialized"));
  Serial.println("Battery: " + String(readBatteryVoltage(), 2) + "V");
}

#endif // SENSORS_H 