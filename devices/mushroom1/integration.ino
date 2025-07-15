/*
 * Mushroom 1 - NatureOS Integration
 * 
 * This firmware integrates the Mushroom 1 device with NatureOS
 * Features:
 * - ESP32 with WiFi connectivity
 * - ADS1299 for bioelectric signal acquisition
 * - BME688 for environmental sensing
 * - Secure IoT Hub communication
 * - Mycorrhizae Protocol implementation
 */

#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include <PubSubClient.h>
#include <time.h>
#include <SPI.h>
#include <Wire.h>
#include "ADS1299.h"
#include "Adafruit_BME680.h"

// WiFi credentials
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";

// Azure IoT Hub configuration
const char* iotHubHostname = "natureos-iothub.azure-devices.net";
const char* deviceId = "mushroom-001";
const char* deviceKey = "YOUR_DEVICE_SAS_KEY";

// Device configuration
#define ADS1299_CS_PIN 5
#define ADS1299_DRDY_PIN 4
#define BME688_SDA_PIN 21
#define BME688_SCL_PIN 22

// Global objects
WiFiClientSecure wifiClient;
PubSubClient mqttClient(wifiClient);
ADS1299 ads;
Adafruit_BME680 bme;

// Sampling configuration
const int SAMPLE_RATE = 250; // Hz
const int CHANNELS = 8;
const int BUFFER_SIZE = 250; // 1 second of data
int sampleBuffer[CHANNELS][BUFFER_SIZE];
int bufferIndex = 0;
unsigned long lastSampleTime = 0;
unsigned long lastTelemetryTime = 0;
const unsigned long TELEMETRY_INTERVAL = 30000; // 30 seconds

// Device state
struct DeviceState {
  bool wifiConnected = false;
  bool iotHubConnected = false;
  bool adsInitialized = false;
  bool bmeInitialized = false;
  float temperature = 0.0;
  float humidity = 0.0;
  float pressure = 0.0;
  float gasResistance = 0.0;
  float vocIndex = 0.0;
  int signalQuality = 0;
} deviceState;

void setup() {
  Serial.begin(115200);
  delay(1000);
  
  Serial.println("Mushroom 1 - NatureOS Integration Starting...");
  
  // Initialize hardware
  initializeHardware();
  
  // Connect to WiFi
  connectToWiFi();
  
  // Configure time
  configureTime();
  
  // Connect to Azure IoT Hub
  connectToIoTHub();
  
  Serial.println("Mushroom 1 initialized successfully!");
}

void loop() {
  // Maintain connections
  if (!WiFi.isConnected()) {
    connectToWiFi();
  }
  
  if (!mqttClient.connected()) {
    connectToIoTHub();
  }
  
  mqttClient.loop();
  
  // Sample bioelectric signals
  sampleBioelectricSignals();
  
  // Read environmental sensors
  readEnvironmentalSensors();
  
  // Send telemetry periodically
  if (millis() - lastTelemetryTime >= TELEMETRY_INTERVAL) {
    sendTelemetryData();
    lastTelemetryTime = millis();
  }
  
  // Small delay to prevent watchdog reset
  delay(1);
}

void initializeHardware() {
  Serial.println("Initializing hardware...");
  
  // Initialize SPI for ADS1299
  SPI.begin();
  pinMode(ADS1299_CS_PIN, OUTPUT);
  pinMode(ADS1299_DRDY_PIN, INPUT);
  digitalWrite(ADS1299_CS_PIN, HIGH);
  
  // Initialize ADS1299
  if (ads.begin(ADS1299_CS_PIN, ADS1299_DRDY_PIN)) {
    Serial.println("ADS1299 initialized successfully");
    deviceState.adsInitialized = true;
    
    // Configure ADS1299 for bioelectric recording
    ads.setGain(ADS1299_GAIN_24);
    ads.setSampleRate(ADS1299_SAMPLE_RATE_250SPS);
    ads.enableAllChannels();
    ads.startContinuousConversion();
  } else {
    Serial.println("Failed to initialize ADS1299");
  }
  
  // Initialize I2C for BME688
  Wire.begin(BME688_SDA_PIN, BME688_SCL_PIN);
  
  // Initialize BME688
  if (bme.begin()) {
    Serial.println("BME688 initialized successfully");
    deviceState.bmeInitialized = true;
    
    // Configure BME688
    bme.setTemperatureOversampling(BME680_OS_8X);
    bme.setHumidityOversampling(BME680_OS_2X);
    bme.setPressureOversampling(BME680_OS_4X);
    bme.setIIRFilterSize(BME680_FILTER_SIZE_3);
    bme.setGasHeater(320, 150); // 320°C for 150 ms
  } else {
    Serial.println("Failed to initialize BME688");
  }
}

void connectToWiFi() {
  if (WiFi.isConnected()) return;
  
  Serial.print("Connecting to WiFi");
  WiFi.begin(ssid, password);
  
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    attempts++;
  }
  
  if (WiFi.isConnected()) {
    Serial.println("\nWiFi connected!");
    Serial.print("IP address: ");
    Serial.println(WiFi.localIP());
    deviceState.wifiConnected = true;
  } else {
    Serial.println("\nFailed to connect to WiFi");
    deviceState.wifiConnected = false;
  }
}

void configureTime() {
  configTime(0, 0, "pool.ntp.org", "time.nist.gov");
  Serial.print("Waiting for NTP time sync: ");
  
  time_t nowSecs = time(nullptr);
  while (nowSecs < 8 * 3600 * 2) {
    delay(500);
    Serial.print(".");
    yield();
    nowSecs = time(nullptr);
  }
  
  Serial.println();
  struct tm timeinfo;
  gmtime_r(&nowSecs, &timeinfo);
  Serial.print("Current time: ");
  Serial.print(asctime(&timeinfo));
}

void connectToIoTHub() {
  if (mqttClient.connected()) return;
  
  Serial.println("Connecting to Azure IoT Hub...");
  
  // Configure secure connection
  wifiClient.setInsecure(); // For development only
  mqttClient.setServer(iotHubHostname, 8883);
  mqttClient.setCallback(messageCallback);
  
  // Generate SAS token (simplified for demo)
  String sasToken = generateSasToken();
  String username = String(iotHubHostname) + "/" + deviceId + "/?api-version=2021-04-12";
  
  if (mqttClient.connect(deviceId, username.c_str(), sasToken.c_str())) {
    Serial.println("Connected to IoT Hub!");
    deviceState.iotHubConnected = true;
    
    // Subscribe to cloud-to-device messages
    String c2dTopic = "devices/" + String(deviceId) + "/messages/devicebound/#";
    mqttClient.subscribe(c2dTopic.c_str());
  } else {
    Serial.print("Failed to connect to IoT Hub, rc=");
    Serial.println(mqttClient.state());
    deviceState.iotHubConnected = false;
  }
}

String generateSasToken() {
  // Simplified SAS token generation
  // In production, implement proper HMAC-SHA256 signature
  return "SharedAccessSignature sr=" + String(iotHubHostname) + 
         "%2Fdevices%2F" + deviceId + "&sig=SIGNATURE&se=EXPIRY";
}

void sampleBioelectricSignals() {
  if (!deviceState.adsInitialized) return;
  
  unsigned long currentTime = micros();
  if (currentTime - lastSampleTime >= (1000000 / SAMPLE_RATE)) {
    lastSampleTime = currentTime;
    
    // Read all channels
    if (ads.isDataReady()) {
      for (int channel = 0; channel < CHANNELS; channel++) {
        int32_t rawValue = ads.readChannel(channel);
        // Convert to microvolts
        float microvolts = ads.rawToMicrovolts(rawValue);
        sampleBuffer[channel][bufferIndex] = (int)microvolts;
      }
      
      bufferIndex = (bufferIndex + 1) % BUFFER_SIZE;
      
      // Calculate signal quality
      deviceState.signalQuality = calculateSignalQuality();
    }
  }
}

void readEnvironmentalSensors() {
  if (!deviceState.bmeInitialized) return;
  
  static unsigned long lastEnvRead = 0;
  if (millis() - lastEnvRead >= 2000) { // Read every 2 seconds
    lastEnvRead = millis();
    
    if (bme.performReading()) {
      deviceState.temperature = bme.temperature;
      deviceState.humidity = bme.humidity;
      deviceState.pressure = bme.pressure / 100.0; // Convert to hPa
      deviceState.gasResistance = bme.gas_resistance / 1000.0; // Convert to KOhms
      
      // Calculate VOC index (simplified)
      deviceState.vocIndex = map(deviceState.gasResistance, 1, 100, 0, 500);
    }
  }
}

void sendTelemetryData() {
  if (!deviceState.iotHubConnected) return;
  
  Serial.println("Sending telemetry data...");
  
  // Create Mycorrhizae Protocol event
  DynamicJsonDocument doc(4096);
  
  // Generate ULID (simplified)
  String eventId = generateULID();
  
  doc["event_id"] = eventId;
  doc["timestamp"] = getISOTimestamp();
  doc["source_device"] = deviceId;
  doc["kingdom_domain"] = "FUNGA.electrical";
  
  // Signal vector (last 10 samples from each channel)
  JsonArray signalVector = doc.createNestedArray("signal_vector");
  JsonObject bioelectricChannels = signalVector.createNestedObject();
  
  for (int channel = 0; channel < CHANNELS; channel++) {
    JsonArray channelData = bioelectricChannels.createNestedArray("channel_" + String(channel));
    
    for (int i = 0; i < 10; i++) {
      int index = (bufferIndex - 10 + i + BUFFER_SIZE) % BUFFER_SIZE;
      channelData.add(sampleBuffer[channel][index]);
    }
  }
  
  // Environmental data
  signalVector[1]["temperature"] = deviceState.temperature;
  signalVector[1]["humidity"] = deviceState.humidity;
  signalVector[1]["pressure"] = deviceState.pressure;
  signalVector[1]["gas_resistance"] = deviceState.gasResistance;
  signalVector[1]["voc_index"] = deviceState.vocIndex;
  
  // References
  JsonObject references = doc.createNestedObject("references");
  JsonObject location = references.createNestedObject("location");
  location["latitude"] = 47.6062;  // Example coordinates
  location["longitude"] = -122.3321;
  location["accuracy"] = 10.0;
  
  JsonObject environment = references.createNestedObject("environment");
  environment["habitat"] = "laboratory";
  environment["substrate"] = "agar_medium";
  environment["temperature"] = deviceState.temperature;
  environment["humidity"] = deviceState.humidity;
  environment["ph"] = 6.5; // Example pH
  
  // Metadata
  JsonObject metadata = doc.createNestedObject("metadata");
  metadata["pipeline_version"] = "1.0.0";
  metadata["ingested_at"] = getISOTimestamp();
  metadata["tenant_id"] = "lab-001";
  metadata["quality_score"] = deviceState.signalQuality / 100.0;
  
  JsonArray flags = metadata.createNestedArray("flags");
  if (deviceState.signalQuality < 70) {
    flags.add("low_signal_quality");
  }
  if (deviceState.temperature > 30) {
    flags.add("high_temperature");
  }
  
  // Serialize and send
  String jsonString;
  serializeJson(doc, jsonString);
  
  String topic = "devices/" + String(deviceId) + "/messages/events/";
  
  if (mqttClient.publish(topic.c_str(), jsonString.c_str())) {
    Serial.println("Telemetry sent successfully");
    Serial.print("Event ID: ");
    Serial.println(eventId);
  } else {
    Serial.println("Failed to send telemetry");
  }
}

void messageCallback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Message received on topic: ");
  Serial.println(topic);
  
  // Parse cloud-to-device message
  DynamicJsonDocument doc(1024);
  deserializeJson(doc, payload, length);
  
  String command = doc["command"];
  
  if (command == "calibrate") {
    Serial.println("Calibration command received");
    calibrateDevice();
  } else if (command == "set_sample_rate") {
    int newRate = doc["sample_rate"];
    Serial.print("Setting new sample rate: ");
    Serial.println(newRate);
    // Implementation would adjust ADS1299 settings
  } else if (command == "get_status") {
    sendDeviceStatus();
  }
}

int calculateSignalQuality() {
  // Calculate signal quality based on noise levels
  float totalNoise = 0;
  int samples = 0;
  
  for (int channel = 0; channel < CHANNELS; channel++) {
    float channelMean = 0;
    float channelStd = 0;
    
    // Calculate mean
    for (int i = 0; i < BUFFER_SIZE; i++) {
      channelMean += sampleBuffer[channel][i];
    }
    channelMean /= BUFFER_SIZE;
    
    // Calculate standard deviation
    for (int i = 0; i < BUFFER_SIZE; i++) {
      float diff = sampleBuffer[channel][i] - channelMean;
      channelStd += diff * diff;
    }
    channelStd = sqrt(channelStd / BUFFER_SIZE);
    
    totalNoise += channelStd;
    samples++;
  }
  
  float avgNoise = totalNoise / samples;
  
  // Convert to quality score (0-100)
  int quality = map(avgNoise, 0, 1000, 100, 0);
  return constrain(quality, 0, 100);
}

void calibrateDevice() {
  Serial.println("Starting device calibration...");
  
  // Calibrate ADS1299
  if (deviceState.adsInitialized) {
    ads.calibrate();
  }
  
  // Calibrate BME688
  if (deviceState.bmeInitialized) {
    // Perform baseline calibration
    for (int i = 0; i < 10; i++) {
      bme.performReading();
      delay(100);
    }
  }
  
  Serial.println("Calibration complete");
  sendDeviceStatus();
}

void sendDeviceStatus() {
  DynamicJsonDocument doc(1024);
  
  doc["device_id"] = deviceId;
  doc["timestamp"] = getISOTimestamp();
  doc["wifi_connected"] = deviceState.wifiConnected;
  doc["iot_hub_connected"] = deviceState.iotHubConnected;
  doc["ads_initialized"] = deviceState.adsInitialized;
  doc["bme_initialized"] = deviceState.bmeInitialized;
  doc["signal_quality"] = deviceState.signalQuality;
  doc["temperature"] = deviceState.temperature;
  doc["humidity"] = deviceState.humidity;
  doc["free_heap"] = ESP.getFreeHeap();
  
  String jsonString;
  serializeJson(doc, jsonString);
  
  String topic = "devices/" + String(deviceId) + "/messages/events/status";
  mqttClient.publish(topic.c_str(), jsonString.c_str());
}

String generateULID() {
  // Simplified ULID generation
  return "01F" + String(millis()) + String(random(100000, 999999));
}

String getISOTimestamp() {
  time_t now = time(nullptr);
  struct tm timeinfo;
  gmtime_r(&now, &timeinfo);
  
  char timestamp[32];
  strftime(timestamp, sizeof(timestamp), "%Y-%m-%dT%H:%M:%S.000Z", &timeinfo);
  
  return String(timestamp);
} 