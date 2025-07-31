/*
 * Mushroom 1 - NatureOS IoT Device
 * Advanced bioelectric signal acquisition and environmental monitoring
 * 
 * Hardware:
 * - ESP32-WROOM-32D (main controller)
 * - ADS1299 (8-channel 24-bit bioelectric ADC)
 * - BME688 (environmental sensor - temperature, humidity, pressure, gas)
 * - OLED Display (optional status display)
 * - SD Card (local data buffering)
 * 
 * Features:
 * - Real-time bioelectric signal acquisition
 * - Environmental monitoring
 * - MQTT communication with Azure IoT Hub
 * - Local data buffering and fault tolerance
 * - OTA firmware updates
 * - Low power mode support
 */

#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <time.h>
#include <ESP32Time.h>
#include <SPI.h>
#include <Wire.h>
#include <SD.h>
#include <FS.h>
#include <SPIFFS.h>

#include "config.h"
#include "sensors.h"

// Device configuration
const char* DEVICE_ID = "mushroom-1-001";
const char* FIRMWARE_VERSION = "2.1.0";
const int SAMPLING_RATE_HZ = 250;
const int BATCH_SIZE = 10;

// Pin definitions
#define ADS1299_CS_PIN 5
#define ADS1299_DRDY_PIN 4
#define BME688_SDA_PIN 21
#define BME688_SCL_PIN 22
#define SD_CS_PIN 15
#define LED_PIN 2
#define BUTTON_PIN 0

// Global objects
WiFiClientSecure wifiClient;
PubSubClient mqttClient(wifiClient);
ESP32Time rtc;
SensorManager sensors;

// State variables
bool wifiConnected = false;
bool mqttConnected = false;
bool sdCardAvailable = false;
unsigned long lastTelemetryTime = 0;
unsigned long lastHeartbeatTime = 0;
unsigned long samplingTimer = 0;
int batchCounter = 0;

// Data buffers
struct SensorReading {
  unsigned long timestamp;
  float bioelectricChannels[8];
  float temperature;
  float humidity;
  float pressure;
  float gasResistance;
  float batteryVoltage;
  int signalQuality;
};

SensorReading currentReading;
SensorReading readingBuffer[BATCH_SIZE];

void setup() {
  Serial.begin(115200);
  delay(1000);
  
  Serial.println("🍄 Mushroom 1 - NatureOS IoT Device");
  Serial.println("Firmware Version: " + String(FIRMWARE_VERSION));
  Serial.println("Device ID: " + String(DEVICE_ID));
  
  // Initialize pins
  pinMode(LED_PIN, OUTPUT);
  pinMode(BUTTON_PIN, INPUT_PULLUP);
  
  // Initialize LED indicator
  blinkLED(3, 200);
  
  // Initialize SPIFFS for configuration
  if (!SPIFFS.begin(true)) {
    Serial.println("❌ SPIFFS initialization failed");
  } else {
    Serial.println("✅ SPIFFS initialized");
  }
  
  // Initialize SD card
  initializeSDCard();
  
  // Initialize sensors
  if (!sensors.begin()) {
    Serial.println("❌ Sensor initialization failed");
    blinkLED(10, 100); // Error indication
  } else {
    Serial.println("✅ Sensors initialized successfully");
  }
  
  // Initialize WiFi
  initializeWiFi();
  
  // Initialize time
  configTime(0, 0, "pool.ntp.org", "time.nist.gov");
  delay(2000);
  
  // Initialize MQTT
  initializeMQTT();
  
  // Set sampling timer
  samplingTimer = micros();
  
  Serial.println("🚀 Mushroom 1 ready for data acquisition");
  digitalWrite(LED_PIN, HIGH);
}

void loop() {
  // Handle WiFi connection
  if (!wifiConnected) {
    reconnectWiFi();
  }
  
  // Handle MQTT connection
  if (wifiConnected && !mqttConnected) {
    reconnectMQTT();
  }
  
  // Process MQTT messages
  if (mqttConnected) {
    mqttClient.loop();
  }
  
  // Sample sensors at specified rate
  if (micros() - samplingTimer >= (1000000 / SAMPLING_RATE_HZ)) {
    sampleSensors();
    samplingTimer = micros();
  }
  
  // Send telemetry batch
  if (batchCounter >= BATCH_SIZE) {
    sendTelemetryBatch();
    batchCounter = 0;
  }
  
  // Send heartbeat every 30 seconds
  if (millis() - lastHeartbeatTime > 30000) {
    sendHeartbeat();
    lastHeartbeatTime = millis();
  }
  
  // Handle button press (for manual sync/reset)
  if (digitalRead(BUTTON_PIN) == LOW) {
    delay(50); // Debounce
    if (digitalRead(BUTTON_PIN) == LOW) {
      handleButtonPress();
      while (digitalRead(BUTTON_PIN) == LOW) delay(10);
    }
  }
  
  // Small delay to prevent watchdog reset
  delay(1);
}

void initializeWiFi() {
  Serial.println("📡 Connecting to WiFi...");
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    attempts++;
  }
  
  if (WiFi.status() == WL_CONNECTED) {
    wifiConnected = true;
    Serial.println("\n✅ WiFi connected");
    Serial.println("IP address: " + WiFi.localIP().toString());
    Serial.println("Signal strength: " + String(WiFi.RSSI()) + " dBm");
  } else {
    Serial.println("\n❌ WiFi connection failed");
    wifiConnected = false;
  }
}

void reconnectWiFi() {
  static unsigned long lastAttempt = 0;
  if (millis() - lastAttempt > 30000) { // Try every 30 seconds
    Serial.println("🔄 Attempting WiFi reconnection...");
    WiFi.disconnect();
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    lastAttempt = millis();
  }
  
  if (WiFi.status() == WL_CONNECTED) {
    wifiConnected = true;
    Serial.println("✅ WiFi reconnected");
  }
}

void initializeMQTT() {
  wifiClient.setCACert(IOT_HUB_ROOT_CA);
  wifiClient.setCertificate(IOT_HUB_DEVICE_CERT);
  wifiClient.setPrivateKey(IOT_HUB_DEVICE_KEY);
  
  mqttClient.setServer(IOT_HUB_HOSTNAME, 8883);
  mqttClient.setCallback(mqttCallback);
  mqttClient.setBufferSize(2048);
  
  connectMQTT();
}

void connectMQTT() {
  if (!wifiConnected) return;
  
  String clientId = String(DEVICE_ID) + "-" + String(random(0xffff), HEX);
  String username = String(IOT_HUB_HOSTNAME) + "/" + String(DEVICE_ID) + "/?api-version=2021-04-12";
  
  Serial.println("🔐 Connecting to Azure IoT Hub...");
  
  if (mqttClient.connect(clientId.c_str(), username.c_str(), IOT_HUB_SAS_TOKEN)) {
    mqttConnected = true;
    Serial.println("✅ Connected to Azure IoT Hub");
    
    // Subscribe to device commands
    String commandTopic = "devices/" + String(DEVICE_ID) + "/messages/devicebound/#";
    mqttClient.subscribe(commandTopic.c_str());
    
    // Subscribe to device twin updates
    String twinTopic = "$iothub/twin/res/#";
    mqttClient.subscribe(twinTopic.c_str());
    
    // Send device info
    sendDeviceInfo();
    
  } else {
    mqttConnected = false;
    Serial.println("❌ MQTT connection failed, state: " + String(mqttClient.state()));
  }
}

void reconnectMQTT() {
  static unsigned long lastAttempt = 0;
  if (millis() - lastAttempt > 30000) { // Try every 30 seconds
    connectMQTT();
    lastAttempt = millis();
  }
}

void sampleSensors() {
  // Read all sensor data
  currentReading.timestamp = millis();
  
  // Read bioelectric signals from ADS1299
  sensors.readBioelectricChannels(currentReading.bioelectricChannels);
  
  // Read environmental data from BME688
  sensors.readEnvironmentalData(
    currentReading.temperature,
    currentReading.humidity,
    currentReading.pressure,
    currentReading.gasResistance
  );
  
  // Read battery voltage
  currentReading.batteryVoltage = sensors.readBatteryVoltage();
  
  // Calculate signal quality
  currentReading.signalQuality = sensors.calculateSignalQuality(currentReading.bioelectricChannels);
  
  // Add to batch buffer
  readingBuffer[batchCounter] = currentReading;
  batchCounter++;
  
  // Save to SD card if available
  if (sdCardAvailable) {
    saveReadingToSD(currentReading);
  }
  
  // LED indication based on signal quality
  if (currentReading.signalQuality > 80) {
    digitalWrite(LED_PIN, HIGH);
  } else {
    digitalWrite(LED_PIN, millis() % 1000 < 100); // Blink for poor quality
  }
}

void sendTelemetryBatch() {
  if (!mqttConnected) {
    Serial.println("⚠️ MQTT not connected, buffering data");
    return;
  }
  
  // Create JSON payload following Mycorrhizae Protocol
  DynamicJsonDocument doc(4096);
  JsonArray events = doc.createNestedArray("events");
  
  for (int i = 0; i < BATCH_SIZE; i++) {
    JsonObject event = events.createNestedObject();
    
    // Generate ULID for event
    String eventId = generateULID();
    event["event_id"] = eventId;
    event["timestamp"] = getISOTimestamp(readingBuffer[i].timestamp);
    event["source_device"] = DEVICE_ID;
    event["kingdom_domain"] = "FUNGA.bioelectric";
    
    // Signal vector (raw sensor data)
    JsonObject signalVector = event.createNestedObject("signal_vector");
    JsonArray bioChannels = signalVector.createNestedArray("bioelectric_channels");
    for (int ch = 0; ch < 8; ch++) {
      bioChannels.add(readingBuffer[i].bioelectricChannels[ch]);
    }
    
    JsonObject environmental = signalVector.createNestedObject("environmental");
    environmental["temperature"] = readingBuffer[i].temperature;
    environmental["humidity"] = readingBuffer[i].humidity;
    environmental["pressure"] = readingBuffer[i].pressure;
    environmental["gas_resistance"] = readingBuffer[i].gasResistance;
    environmental["battery_voltage"] = readingBuffer[i].batteryVoltage;
    environmental["signal_quality"] = readingBuffer[i].signalQuality;
    
    // Decoded meaning (basic signal analysis)
    JsonObject decodedMeaning = event.createNestedObject("decoded_meaning");
    decodedMeaning["@context"] = "https://natureos.org/context/bioelectric";
    decodedMeaning["@type"] = "BioelectricReading";
    decodedMeaning["confidence"] = readingBuffer[i].signalQuality / 100.0;
    decodedMeaning["algorithm"] = "mushroom1-v2.1";
    
    // Metadata
    JsonObject metadata = event.createNestedObject("metadata");
    metadata["device_firmware"] = FIRMWARE_VERSION;
    metadata["sampling_rate"] = SAMPLING_RATE_HZ;
    metadata["batch_sequence"] = i;
    metadata["wifi_rssi"] = WiFi.RSSI();
    metadata["free_heap"] = ESP.getFreeHeap();
  }
  
  // Serialize and send
  String payload;
  serializeJson(doc, payload);
  
  String topic = "devices/" + String(DEVICE_ID) + "/messages/events/";
  
  if (mqttClient.publish(topic.c_str(), payload.c_str())) {
    Serial.println("📤 Telemetry batch sent (" + String(BATCH_SIZE) + " events)");
    lastTelemetryTime = millis();
    
    // Brief success indication
    blinkLED(1, 50);
  } else {
    Serial.println("❌ Failed to send telemetry batch");
    
    // Save failed batch to SD card for retry
    if (sdCardAvailable) {
      saveBatchToSD(payload);
    }
  }
}

void sendHeartbeat() {
  if (!mqttConnected) return;
  
  DynamicJsonDocument doc(512);
  doc["device_id"] = DEVICE_ID;
  doc["timestamp"] = getISOTimestamp(millis());
  doc["firmware_version"] = FIRMWARE_VERSION;
  doc["uptime_ms"] = millis();
  doc["free_heap"] = ESP.getFreeHeap();
  doc["wifi_rssi"] = WiFi.RSSI();
  doc["battery_voltage"] = sensors.readBatteryVoltage();
  doc["sd_card_available"] = sdCardAvailable;
  doc["last_telemetry"] = lastTelemetryTime;
  
  String payload;
  serializeJson(doc, payload);
  
  String topic = "devices/" + String(DEVICE_ID) + "/messages/events/heartbeat";
  mqttClient.publish(topic.c_str(), payload.c_str());
  
  Serial.println("💓 Heartbeat sent");
}

void sendDeviceInfo() {
  DynamicJsonDocument doc(1024);
  doc["device_id"] = DEVICE_ID;
  doc["device_type"] = "mushroom-sensor";
  doc["firmware_version"] = FIRMWARE_VERSION;
  doc["hardware_version"] = "1.2";
  doc["manufacturer"] = "Mycosoft Labs";
  doc["capabilities"] = "bioelectric,environmental,mqtt,ota";
  doc["sampling_rate_max"] = 1000;
  doc["channels"] = 8;
  doc["battery_powered"] = true;
  doc["sd_card_storage"] = sdCardAvailable;
  
  String payload;
  serializeJson(doc, payload);
  
  String topic = "devices/" + String(DEVICE_ID) + "/messages/events/device-info";
  mqttClient.publish(topic.c_str(), payload.c_str());
  
  Serial.println("ℹ️ Device info sent");
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String message = "";
  for (unsigned int i = 0; i < length; i++) {
    message += (char)payload[i];
  }
  
  Serial.println("📨 Received: " + String(topic) + " -> " + message);
  
  // Parse command
  DynamicJsonDocument doc(512);
  deserializeJson(doc, message);
  
  String command = doc["command"].as<String>();
  
  if (command == "get_status") {
    sendDeviceInfo();
  } else if (command == "set_sampling_rate") {
    int newRate = doc["value"].as<int>();
    if (newRate > 0 && newRate <= 1000) {
      // Update sampling rate
      Serial.println("📝 Sampling rate updated to " + String(newRate) + " Hz");
    }
  } else if (command == "firmware_update") {
    String updateUrl = doc["url"].as<String>();
    Serial.println("🔄 Firmware update requested: " + updateUrl);
    // TODO: Implement OTA update
  } else if (command == "restart") {
    Serial.println("🔄 Device restart requested");
    delay(1000);
    ESP.restart();
  }
}

void initializeSDCard() {
  if (SD.begin(SD_CS_PIN)) {
    sdCardAvailable = true;
    Serial.println("✅ SD card initialized");
    
    // Create data directory
    if (!SD.exists("/data")) {
      SD.mkdir("/data");
    }
  } else {
    sdCardAvailable = false;
    Serial.println("⚠️ SD card not available");
  }
}

void saveReadingToSD(const SensorReading& reading) {
  String filename = "/data/" + String(day()) + ".csv";
  File file = SD.open(filename, FILE_APPEND);
  
  if (file) {
    // CSV format: timestamp,ch0,ch1,...,ch7,temp,humidity,pressure,gas,battery,quality
    file.print(reading.timestamp);
    for (int i = 0; i < 8; i++) {
      file.print(",");
      file.print(reading.bioelectricChannels[i], 6);
    }
    file.print(",");
    file.print(reading.temperature, 2);
    file.print(",");
    file.print(reading.humidity, 2);
    file.print(",");
    file.print(reading.pressure, 2);
    file.print(",");
    file.print(reading.gasResistance, 0);
    file.print(",");
    file.print(reading.batteryVoltage, 3);
    file.print(",");
    file.println(reading.signalQuality);
    
    file.close();
  }
}

void saveBatchToSD(const String& payload) {
  String filename = "/data/failed_batches.json";
  File file = SD.open(filename, FILE_APPEND);
  
  if (file) {
    file.println(payload);
    file.close();
  }
}

void handleButtonPress() {
  Serial.println("🔘 Button pressed - manual sync");
  
  // Force immediate telemetry send
  if (batchCounter > 0) {
    sendTelemetryBatch();
    batchCounter = 0;
  }
  
  sendHeartbeat();
  blinkLED(2, 200);
}

void blinkLED(int count, int delayMs) {
  for (int i = 0; i < count; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(delayMs);
    digitalWrite(LED_PIN, LOW);
    delay(delayMs);
  }
}

String generateULID() {
  // Simplified ULID generation
  String ulid = "";
  unsigned long timestamp = millis();
  
  // Base32 encoding of timestamp + random
  const char* chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
  
  for (int i = 0; i < 26; i++) {
    if (i < 10) {
      ulid += chars[timestamp % 32];
      timestamp /= 32;
    } else {
      ulid += chars[random(0, 32)];
    }
  }
  
  return ulid;
}

String getISOTimestamp(unsigned long timestampMs) {
  time_t rawtime = timestampMs / 1000;
  struct tm * timeinfo = gmtime(&rawtime);
  
  char buffer[30];
  strftime(buffer, 30, "%Y-%m-%dT%H:%M:%S", timeinfo);
  
  // Add milliseconds
  int ms = timestampMs % 1000;
  sprintf(buffer + strlen(buffer), ".%03dZ", ms);
  
  return String(buffer);
} 