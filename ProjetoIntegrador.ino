#include <ArduinoJson.h>
#include <DHT.h>
#include <math.h>
#include <ESP8266WiFi.h>
#include <PubSubClient.h>

// Definições do sensor DHT11
#define PINO_DHT D4
#define TIPO_DHT DHT11
DHT dht(PINO_DHT, TIPO_DHT);

// Definições de pinos
const int PINO_LED_PLACA = D4;
const int PINO_SENSOR_MQ7 = D5;
const int PINO_SENSOR_CHAMA = D3;
const int PINO_SENSOR_MQ2 = A0;

// Intervalo de leitura
const int INTERVALO_LEITURA_MS = 1000;

// Credenciais Wi-Fi
const char* ssid = "SENAC";
const char* senha = "x1y2z3@snc";

// Servidor MQTT
const char* endereco_mqtt = "10.10.29.26";
const int porta_mqtt = 1883;

WiFiClient espClient;
PubSubClient clienteMQTT(espClient);

// Variáveis do sensor MQ-2
#define RESISTENCIA_CARGA 10.0  // em kOhm
#define NUM_AMOSTRAS 10
int leiturasGas[NUM_AMOSTRAS];
int indiceGas = 0;
int totalGas = 0;
float Ro = 10.0; // valor inicial arbitrário

// === Funções ===

void calibrarSensorMQ2() {
  Serial.println("⏳ Calibrando sensor MQ-2 em ar limpo...");
  verificarConexaoMQTT();

  StaticJsonDocument<200> doc;
  doc["calibrando"] = true;
  char bufferJson[256];
  serializeJson(doc, bufferJson);
  clienteMQTT.publish("sensor/calibrando", bufferJson);

  float somaRs = 0;
  for (int i = 0; i < 50; i++) {
    int leituraADC = analogRead(PINO_SENSOR_MQ2);
    float tensaoSaida = leituraADC * (5.0 / 1023.0);
    float Rs = (5.0 - tensaoSaida) / tensaoSaida * RESISTENCIA_CARGA;
    somaRs += Rs;
    delay(100);
  }

  float RsMedio = somaRs / 50;
  Ro = RsMedio / 9.83;  // valor típico em ar limpo para GLP

  Serial.print("✅ Calibração finalizada! Ro = ");
  Serial.print(Ro);
  Serial.println(" kΩ");

  doc["calibrando"] = false;
  doc["Ro"] = Ro;
  doc["Rs"] = RsMedio;
  serializeJson(doc, bufferJson);
  clienteMQTT.publish("sensor/calibrando", bufferJson);
}

float calcularPPM(float razaoRsRo) {
  float m = -0.47;
  float b = 1.95;
  return pow(10, ((log10(razaoRsRo) - b) / m));
}

void enviarDadosParaMQTT(int valorAnalogico, float ppm, bool chama, float temperatura, float umidade, bool coDetectado) {
  Serial.println("📡 Enviando dados ao broker...");

  StaticJsonDocument<200> doc;
  doc["valorAnalogico_MQ2"] = valorAnalogico;
  doc["ppm_MQ2"] = ppm;
  doc["chamaDetectada"] = chama;
  doc["temperatura"] = temperatura;
  doc["umidade"] = umidade;
  doc["coDetectado"] = coDetectado;

  char bufferJson[256];
  serializeJson(doc, bufferJson);
  clienteMQTT.publish("sensor/bmp280", bufferJson);
}

void verificarConexaoMQTT() {
  if (!clienteMQTT.connected()) {
    reconectarMQTT();
  }
  clienteMQTT.loop();
}

void reconectarMQTT() {
  while (!clienteMQTT.connected()) {
    Serial.print("🔁 Tentando conectar ao MQTT... ");
    String clientId = "ESP8266Client-" + String(random(0xffff), HEX);
    if (clienteMQTT.connect(clientId.c_str())) {
      Serial.println("Conectado!");
    } else {
      Serial.print("Falha, rc=");
      Serial.print(clienteMQTT.state());
      Serial.println(" | Tentando novamente em 5s...");
      delay(5000);
    }
  }
}

// === Setup ===
void setup() {
  Serial.begin(115200);

  WiFi.begin(ssid, senha);
  Serial.print("Conectando ao Wi-Fi");
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\n✅ Wi-Fi conectado!");
  Serial.print("IP do ESP8266: ");
  Serial.println(WiFi.localIP());

  clienteMQTT.setServer(endereco_mqtt, porta_mqtt);
  dht.begin();

  pinMode(PINO_LED_PLACA, OUTPUT);
  pinMode(PINO_SENSOR_CHAMA, INPUT);
  pinMode(PINO_SENSOR_MQ7, INPUT);

  calibrarSensorMQ2();
}

// === Loop principal ===
void loop() {
  verificarConexaoMQTT();

  // Pisca LED
  digitalWrite(PINO_LED_PLACA, HIGH);
  delay(300);
  digitalWrite(PINO_LED_PLACA, LOW);

  // Leitura do MQ-2
  totalGas -= leiturasGas[indiceGas];
  leiturasGas[indiceGas] = analogRead(PINO_SENSOR_MQ2);
  totalGas += leiturasGas[indiceGas];
  indiceGas = (indiceGas + 1) % NUM_AMOSTRAS;
  int mediaGas = totalGas / NUM_AMOSTRAS;
  if (mediaGas < 100) mediaGas = 0;

  float tensao = mediaGas * (5.0 / 1023.0);
  float Rs = (tensao > 0) ? (5.0 - tensao) / tensao * RESISTENCIA_CARGA : 0;
  float razao = (Ro > 0) ? Rs / Ro : 0;
  float ppm = (razao > 0) ? calcularPPM(razao) : 0;

  Serial.print("📈 MQ-2 (média): ");
  Serial.print(mediaGas);
  Serial.print(" | Rs/Ro: ");
  Serial.print(razao);
  Serial.print(" | Estimado (ppm): ");
  Serial.println(ppm);

  // Leitura da chama
  bool chamaDetectada = digitalRead(PINO_SENSOR_CHAMA) == LOW;
  Serial.println(chamaDetectada ? "🔥 Chama detectada!" : "✅ Sem chama.");

  // Leitura do DHT11
  float umidade = dht.readHumidity();
  float temperatura = dht.readTemperature();
  if (isnan(umidade) || isnan(temperatura)) {
    Serial.println("⚠️ Erro ao ler DHT11.");
  } else {
    Serial.print("🌡️ Temp: ");
    Serial.print(temperatura);
    Serial.println(" °C");

    Serial.print("💧 Umidade: ");
    Serial.print(umidade);
    Serial.println(" %");
  }

  // Leitura do MQ-7
  bool coDetectado = digitalRead(PINO_SENSOR_MQ7) == LOW;
  Serial.println(coDetectado ? "⚠️ Monóxido de carbono detectado!" : "✅ Sem gás tóxico.");

  // Envia dados via MQTT
  enviarDadosParaMQTT(mediaGas, ppm, chamaDetectada, temperatura, umidade, coDetectado);
  delay(INTERVALO_LEITURA_MS);
}
