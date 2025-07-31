/*
 * Mushroom 1 Device Configuration
 * Contains WiFi, Azure IoT Hub, and hardware configuration
 */

#ifndef CONFIG_H
#define CONFIG_H

// WiFi Configuration
#define WIFI_SSID "YourWiFiSSID"
#define WIFI_PASSWORD "YourWiFiPassword"

// Azure IoT Hub Configuration
#define IOT_HUB_HOSTNAME "natureos-iothub-production.azure-devices.net"
#define IOT_HUB_DEVICE_ID "mushroom-1-001"

// Device authentication (these should be unique per device)
// In production, these would be stored in secure element or provisioning service
#define IOT_HUB_SAS_TOKEN "SharedAccessSignature sr=natureos-iothub-production.azure-devices.net%2Fdevices%2Fmushroom-1-001&sig=PLACEHOLDER&se=EXPIRES"

// Root CA certificate for Azure IoT Hub
const char IOT_HUB_ROOT_CA[] = R"EOF(
-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65TANBgkqhkiG9w0BAQsFADA2
MTQwMgYDVQQDDCtNaWNyb3NvZnQgQXp1cmUgSW9UIEh1YiBDQSBDZXJ0aWZpY2F0
ZSBUZXN0MB4XDTE5MDQyNDE0NTU0MVoXDTM5MDQyNDE1MDU0MVowNjE0MDIGA1UE
AwwrTWljcm9zb2Z0IEF6dXJlIElvVCBIdWIgQ0EgQ2VydGlmaWNhdGUgVGVzdDCC
ASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMIzMOJAvDx5ZsQJA+q4KU3M
...)
-----END CERTIFICATE-----
)EOF";

// Device certificate (X.509 authentication)
const char IOT_HUB_DEVICE_CERT[] = R"EOF(
-----BEGIN CERTIFICATE-----
MIIChjCCAW4CAQAwDQYJKoZIhvcNAQEFBQAwEjEQMA4GA1UEAwwHbXlDZXJ0MB4X
DTIzMDEwMTAwMDAwMFoXDTI0MDEwMTAwMDAwMFowEjEQMA4GA1UEAwwHbXlDZXJ0
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...
-----END CERTIFICATE-----
)EOF";

// Device private key (X.509 authentication)
const char IOT_HUB_DEVICE_KEY[] = R"EOF(
-----BEGIN PRIVATE KEY-----
MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC...
-----END PRIVATE KEY-----
)EOF";

// Hardware Configuration
#define ADS1299_CHANNEL_COUNT 8
#define ADC_RESOLUTION_BITS 24
#define ADC_REFERENCE_VOLTAGE 4.5
#define ADC_GAIN_SETTING 24

// BME688 Configuration
#define BME688_I2C_ADDRESS 0x77
#define BME688_TEMP_OFFSET 0.0
#define BME688_HUMIDITY_OFFSET 0.0

// Battery monitoring
#define BATTERY_ADC_PIN A0
#define BATTERY_VOLTAGE_DIVIDER 2.0
#define BATTERY_MIN_VOLTAGE 3.2
#define BATTERY_MAX_VOLTAGE 4.2

// Data buffering
#define MAX_OFFLINE_SAMPLES 10000
#define SD_CARD_LOG_INTERVAL 3600000  // 1 hour in ms

// Signal processing
#define SIGNAL_FILTER_ENABLED true
#define HIGHPASS_CUTOFF_HZ 0.5
#define LOWPASS_CUTOFF_HZ 100.0
#define NOTCH_FILTER_60HZ true

// Power management
#define DEEP_SLEEP_ENABLED false
#define SLEEP_DURATION_SECONDS 300
#define LOW_BATTERY_THRESHOLD 3.4

// Calibration values (device-specific)
#define CALIBRATION_OFFSET_CH0 0.0
#define CALIBRATION_OFFSET_CH1 0.0
#define CALIBRATION_OFFSET_CH2 0.0
#define CALIBRATION_OFFSET_CH3 0.0
#define CALIBRATION_OFFSET_CH4 0.0
#define CALIBRATION_OFFSET_CH5 0.0
#define CALIBRATION_OFFSET_CH6 0.0
#define CALIBRATION_OFFSET_CH7 0.0

#define CALIBRATION_GAIN_CH0 1.0
#define CALIBRATION_GAIN_CH1 1.0
#define CALIBRATION_GAIN_CH2 1.0
#define CALIBRATION_GAIN_CH3 1.0
#define CALIBRATION_GAIN_CH4 1.0
#define CALIBRATION_GAIN_CH5 1.0
#define CALIBRATION_GAIN_CH6 1.0
#define CALIBRATION_GAIN_CH7 1.0

// Quality assessment thresholds
#define SIGNAL_QUALITY_EXCELLENT 90
#define SIGNAL_QUALITY_GOOD 70
#define SIGNAL_QUALITY_POOR 40

// Device identification
#define HARDWARE_VERSION "1.2"
#define MANUFACTURER "Mycosoft Labs"
#define PRODUCT_NAME "Mushroom 1 Bioelectric Sensor"
#define PRODUCT_SKU "M1-BE-001"

// Feature flags
#define FEATURE_OTA_UPDATES true
#define FEATURE_LOCAL_DISPLAY false
#define FEATURE_AUDIO_FEEDBACK false
#define FEATURE_GPS_LOCATION false
#define FEATURE_ENVIRONMENTAL_SENSORS true
#define FEATURE_BIOELECTRIC_SENSORS true

// Network configuration
#define MQTT_KEEPALIVE 60
#define MQTT_QOS 1
#define WIFI_TIMEOUT_MS 10000
#define MQTT_TIMEOUT_MS 5000

// Data transmission
#define TELEMETRY_INTERVAL_MS 5000
#define HEARTBEAT_INTERVAL_MS 30000
#define BATCH_UPLOAD_SIZE 10
#define MAX_RETRY_ATTEMPTS 3

// Debugging
#define DEBUG_SERIAL_ENABLED true
#define DEBUG_LEVEL 2  // 0=None, 1=Error, 2=Info, 3=Debug, 4=Verbose

// Safety limits
#define MAX_CURRENT_INJECTION_UA 50
#define MAX_ELECTRODE_IMPEDANCE_KOHM 100
#define THERMAL_SHUTDOWN_TEMP_C 70

// Environment specific settings
#if defined(ENVIRONMENT_PRODUCTION)
  #define DEFAULT_SAMPLING_RATE 250
  #define SAFETY_CHECKS_ENABLED true
  #define CALIBRATION_REQUIRED true
#elif defined(ENVIRONMENT_DEVELOPMENT)
  #define DEFAULT_SAMPLING_RATE 100
  #define SAFETY_CHECKS_ENABLED false
  #define CALIBRATION_REQUIRED false
#else
  #define DEFAULT_SAMPLING_RATE 250
  #define SAFETY_CHECKS_ENABLED true
  #define CALIBRATION_REQUIRED true
#endif

// Validation macros
#if DEFAULT_SAMPLING_RATE > 1000
  #error "Sampling rate too high - maximum is 1000 Hz"
#endif

#if ADS1299_CHANNEL_COUNT > 8
  #error "ADS1299 supports maximum 8 channels"
#endif

// Helper macros
#define ARRAY_SIZE(arr) (sizeof(arr) / sizeof(arr[0]))
#define MIN(a, b) ((a) < (b) ? (a) : (b))
#define MAX(a, b) ((a) > (b) ? (a) : (b))
#define CONSTRAIN(val, min_val, max_val) (MAX(MIN(val, max_val), min_val))

// Version info
#define CONFIG_VERSION "2.1.0"
#define CONFIG_BUILD_DATE __DATE__ " " __TIME__

#endif // CONFIG_H 