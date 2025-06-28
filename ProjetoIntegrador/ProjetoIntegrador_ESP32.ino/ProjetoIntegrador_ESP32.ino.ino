#include <ArduinoJson.h>
#include <DHT.h>
#include <math.h>
#include <WiFi.h>
#include <PubSubClient.h>

// === Configurações de WiFi e MQTT ===
const char* ssid       = "SENAC";
const char* senha      = "x1y2z3@snc";
const char* mqttServer = "10.10.28.172";
const int   mqttPort   = 1883;

WiFiClient espClient;
PubSubClient clienteMQTT(espClient);

// === Sensores e pinos ===
#define PINO_DHT       33    // DHT11 no GPIO33
#define TIPO_DHT       DHT11
DHT dht(PINO_DHT, TIPO_DHT);

const int PINO_LED_PLACA = 2;   // LED interno ESP32
const int PINO_MQ2       = 32;  // MQ-2 no GPIO32 (analógico)
const int PINO_MQ7       = 35;  // MQ-7 no GPIO35 (analógico)
const int PINO_CHAMA     = 4;   // KY-026 no GPIO4

// === Parâmetros do MQ ===
#define RESISTENCIA_CARGA 10.0  // kΩ (RL = 10 kΩ)
#define NUM_AMOSTRAS      10    // média móvel

float leiturasMQ2[NUM_AMOSTRAS];
float leiturasMQ7[NUM_AMOSTRAS];
int   idx2 = 0, idx7 = 0;
float soma2 = 0, soma7 = 0;
float Ro2 = 10.0; // Ro calibrado MQ-2 (ar limpo / 9.83)
float Ro7 = 0.0;  // Ro calibrado MQ-7 (definido abaixo)

// Intervalo de leitura
const int INTERVALO_LEITURA_MS = 1000;

// === Protótipos ===
void setup();
void loop();
void conectaWiFi();
void reconnectMQTT();
void calibrarMQ2();
void calibrarMQ7();
float calcularPPM_MQ2(float ratio);
float calcularPPM_MQ7(float ratio);
void enviarParaMQTT(float ppm2, float ppm7, bool chama, float temp, float hum);

// === Setup ===
void setup() {
  Serial.begin(115200);
  pinMode(PINO_LED_PLACA, OUTPUT);
  pinMode(PINO_CHAMA, INPUT);
  conectaWiFi();
  clienteMQTT.setServer(mqttServer, mqttPort);
  dht.begin();
  // Calibrar sensores em ar limpo
  calibrarMQ2();
  calibrarMQ7();
}

// === Loop principal ===
void loop() {
  if (!clienteMQTT.connected()) reconnectMQTT();
  clienteMQTT.loop();

  digitalWrite(PINO_LED_PLACA, HIGH);
  delay(100);
  digitalWrite(PINO_LED_PLACA, LOW);

  // --- MQ-2 ---
  soma2 -= leiturasMQ2[idx2];
  leiturasMQ2[idx2] = analogRead(PINO_MQ2);
  soma2 += leiturasMQ2[idx2];
  idx2 = (idx2 + 1) % NUM_AMOSTRAS;
  float media2 = soma2 / NUM_AMOSTRAS;
  float v2 = media2 * (3.3 / 4095.0);                 // ESP32: 12 bits, 3.3V
  float Rs2 = (3.3 - v2) / v2 * RESISTENCIA_CARGA;     // Rs = (Vc/Vrl - 1) * RL
  float ratio2 = Rs2 / Ro2;
  float ppm2   = calcularPPM_MQ2(ratio2);

  // --- MQ-7 ---
  soma7 -= leiturasMQ7[idx7];
  leiturasMQ7[idx7] = analogRead(PINO_MQ7);
  soma7 += leiturasMQ7[idx7];
  idx7 = (idx7 + 1) % NUM_AMOSTRAS;
  float media7 = soma7 / NUM_AMOSTRAS;
  float v7     = media7 * (3.3 / 4095.0);
  float Rs7    = (3.3 - v7) / v7 * RESISTENCIA_CARGA;
  float ratio7 = Rs7 / Ro7;
  float ppm7   = calcularPPM_MQ7(ratio7);

  // --- Outros sensores ---
  bool chamaDetectada = digitalRead(PINO_CHAMA) == LOW;
  float umidade       = dht.readHumidity();
  float temperatura   = dht.readTemperature();

  Serial.printf("MQ-2: %.2f ppm | MQ-7(CO): %.2f ppm\n", ppm2, ppm7);
  Serial.println(chamaDetectada ? "Chama detectada!" : "Sem chama.");
  Serial.printf("Temp: %.1f °C | Umid: %.1f %%\n", temperatura, umidade);

  enviarParaMQTT(ppm2, ppm7, chamaDetectada, temperatura, umidade);
  delay(INTERVALO_LEITURA_MS);
}

// === Conexões ===
void conectaWiFi() {
  WiFi.begin(ssid, senha);
  Serial.print("Conectando ao Wi-Fi");
  while (WiFi.status() != WL_CONNECTED) { delay(500); Serial.print("."); }
  Serial.printf("\nConectado! IP: %s\n", WiFi.localIP().toString().c_str());
}

void reconnectMQTT() {
  while (!clienteMQTT.connected()) {
    Serial.print("Tentando MQTT...");
    if (clienteMQTT.connect("ESP32Client")) Serial.println("Conectado MQTT");
    else { Serial.printf("Erro rc=%d\n", clienteMQTT.state()); delay(2000); }
  }
}

// === Calibração MQ ===
void calibrarMQ2() {
  Serial.println("Calibrando MQ-2 em ar limpo...");
  float sum = 0;
  for (int i = 0; i < 50; i++) {
    float v = analogRead(PINO_MQ2) * (3.3/4095.0);
    float Rs = (3.3 - v)/v * RESISTENCIA_CARGA;
    sum += Rs;
    delay(100);
  }
  Ro2 = (sum/50) / 9.83;  // 9.83: valor típico Rs/Ro em ar limpo (MQ-2 p/ GLP) fileciteturn0file1
  Serial.printf("Ro2 = %.2f kΩ\n", Ro2);
}

void calibrarMQ7() {
  Serial.println("Calibrando MQ-7 em ar limpo...");
  float sum = 0;
  for (int i = 0; i < 50; i++) {
    float v = analogRead(PINO_MQ7) * (3.3/4095.0);
    float Rs = (3.3 - v)/v * RESISTENCIA_CARGA;
    sum += Rs;
    delay(100);
  }
  // Sensitivity S = Rs(air)/Rs(100ppmCO) ≥ 5 fileciteturn0file0
  Ro7 = (sum/50) / 5.0;
  Serial.printf("Ro7 = %.2f kΩ\n", Ro7);
}

// === Cálculo de PPM ===
float calcularPPM_MQ2(float ratio) {
  const float m = -0.47;
  const float b = 1.95;
  return pow(10, (log10(ratio) - b) / m);  // curva MQ-2 p/ GLP fileciteturn0file1
}

float calcularPPM_MQ7(float ratio) {
  // coeficientes aproximados para MQ-7 (CO) a partir da Fig.1:
  const float m = -0.77;
  const float b = 2.02;
  return pow(10, (log10(ratio) - b) / m);  // curva MQ-7 CO fileciteturn0file0
}

// === Envio MQTT ===
void enviarParaMQTT(float ppm2, float ppm7, bool chama, float temp, float hum) {
  StaticJsonDocument<256> doc;
  doc["ppm_MQ2"]   = ppm2;
  doc["ppm_CO_MQ7"] = ppm7;
  doc["chamaDetectada"] = chama;
  doc["temperatura"]= temp;
  doc["umidade"]    = hum;
  char buf[256]; serializeJson(doc, buf);
  clienteMQTT.publish("sensor/valores", buf);
}
