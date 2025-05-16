#include <ArduinoJson.h>
#include <DHT.h>
#include <math.h>
#define DHTPIN D4
#define DHTTYPE DHT11
#include <ESP8266WiFi.h>
#include <PubSubClient.h>

DHT dht(DHTPIN, DHTTYPE);
 
const int PIN_PLACALED = D4;
const int PIN_SENSOR_MQ7 = D5;
const int MS_TEMPO_LEITURA = 1000;

// Defina suas credenciais Wi-Fi
const char* ssid = "SSID";
const char* password = "SENHA";

// Defina o endereço do seu broker MQTT
const char* mqtt_server = "IP";
const int mqtt_port = 0000;

WiFiClient espClient;
PubSubClient client(espClient);
 
#pragma region [VARIAVEIS MQ-2]
#define MQ_ANALOG_PIN A0
#define RL_VALUE 10.0  // kOhm
#define NUM_SAMPLES 10
int gasReadings[NUM_SAMPLES];
int gasIndex = 0;
int totalGas = 0;
float Ro = 10.0; // valor inicial arbitrário
#pragma endregion
 
#pragma region [VARIAVEIS KY-026]
const int PIN_KY_026 = D3;
#pragma endregion
 
void calibrateSensor() {
  Serial.println("⏳ Calibrando sensor MQ (em ar limpo)...");
  float rs_sum = 0;
 
  for (int i = 0; i < 50; i++) {
    int adc = analogRead(MQ_ANALOG_PIN);
    float vout = adc * (5.0 / 1023.0);
    float rs = (5.0 - vout) / vout * RL_VALUE;
    rs_sum += rs;
    delay(100);
  }
 
  float Rs = rs_sum / 50;
  Ro = Rs / 9.83;  // valor típico em ar limpo para GLP
  Serial.print("✅ Calibração finalizada! Ro = ");
  Serial.print(Ro);
  Serial.println(" kΩ");
}
 
float calcularPPM(float rs_ro_ratio) {
  // Curva para GLP (do datasheet do MQ-2)
  float m = -0.47;
  float b = 1.95;
  return pow(10, ((log10(rs_ro_ratio) - b) / m));
}
 
void enviarLeituraMQTT(int valorAnalogico_MQ_2, float ppmMQ_2, int chamaDetectada, float temperatura, float umidade, int monoxidoCarbonoDetectado){
  Serial.println("enviando ao Broker");

 // Cria um objeto JSON
  StaticJsonDocument<200> doc;
  doc["ValorAnalogico_MQ-2"] = valorAnalogico_MQ_2;
  doc["ppm_MQ-2"] = ppmMQ_2;
  doc["chamaDetectada"] = chamaDetectada;
  doc["temperatura"] = temperatura;
  doc["umidade"] = umidade;
  doc["monoxidoCarbonoDetectado"] = monoxidoCarbonoDetectado;

  // Serializa o JSON em uma string
  char messageBuffer[256];
  serializeJson(doc, messageBuffer); // Serializa o JSON diretamente no buffer

  client.publish("sensor/bmp280", messageBuffer);
}

void verificarConexaoMQTT(){
  if (!client.connected()) {
    reconnect();
  }
  client.loop();
}

void reconnect() {
  while (!client.connected()) {
    Serial.print("Tentando conexão MQTT...");
    
    // Gerar um clientId único
    String clientId = "ESP8266Client-" + String(random(0xffff), HEX);
    
    // Tenta conectar com clientId válido
    if (client.connect(clientId.c_str())) {
      Serial.println("Conectado!");
    } else {
      Serial.print("Falha na conexão, rc=");
      Serial.print(client.state());
      Serial.println(" Tente novamente em 5 segundos");
      delay(5000);
    }
  }
}

void setup() {
  Serial.begin(115200);

   // Conecta ao Wi-Fi
   WiFi.begin(ssid, password);
   while (WiFi.status() != WL_CONNECTED) {
     delay(500);
     Serial.print(".");
   }
   Serial.println("Conectado ao Wi-Fi!");
   Serial.print("IP do ESP8266: ");
   Serial.println(WiFi.localIP());
 
   // Conecta ao broker MQTT
   client.setServer(mqtt_server, mqtt_port);

  dht.begin();
 
  pinMode(PIN_PLACALED, OUTPUT);
  pinMode(PIN_KY_026, INPUT);
  pinMode(PIN_SENSOR_MQ7, INPUT);
 
  calibrateSensor(); // MQ analógico
}
 
void loop() {
  verificarConexaoMQTT();

  #pragma region [PISCAR PLACA]
  digitalWrite(PIN_PLACALED, HIGH);
  delay(300);
  digitalWrite(PIN_PLACALED, LOW);
  #pragma endregion
 
  #pragma region [LEITURA MQ-2]
  totalGas -= gasReadings[gasIndex];
  gasReadings[gasIndex] = analogRead(MQ_ANALOG_PIN);
  totalGas += gasReadings[gasIndex];
 
  gasIndex = (gasIndex + 1) % NUM_SAMPLES;
  int averageGas = totalGas / NUM_SAMPLES;
 
  if (averageGas < 100) averageGas = 0;
 
  float vout = averageGas * (5.0 / 1023.0);
  float Rs = (vout > 0) ? (5.0 - vout) / vout * RL_VALUE : 0;
  float ratio = (Ro > 0) ? Rs / Ro : 0;
  float ppm = (ratio > 0) ? calcularPPM(ratio) : 0;
 
  Serial.print("📈 Leitura MQ-2 (média): ");
  Serial.print(averageGas);
  Serial.print(" | Rs/Ro: ");
  Serial.print(ratio);
  Serial.print(" | Estimativa em ppm (GLP): ");
  Serial.println(ppm);
  #pragma endregion
 
  #pragma region [LEITURA KY-026]
  int flameDetected = digitalRead(PIN_KY_026);
  bool chamaDetectada = flameDetected == LOW;
  if (chamaDetectada) {
    Serial.println("🔥 Chama detectada!");
  } else {
    Serial.println("✅ Sem chama.");
  }
  #pragma endregion
 
  #pragma region [LEITURA DHT11]
  float humidity = dht.readHumidity();
  float temperature = dht.readTemperature();
 
  if (isnan(humidity) || isnan(temperature)) {
    Serial.println("⚠️ Erro ao ler DHT11.");
  } else {
    Serial.print("🌡️ Temperatura: ");
    Serial.print(temperature);
    Serial.println(" °C");
 
    Serial.print("💧 Umidade: ");
    Serial.print(humidity);
    Serial.println(" %");
  }
  #pragma endregion
 
  #pragma region [LEITURA MQ-7]
  int gasDetected = digitalRead(PIN_SENSOR_MQ7);
  bool monoxidoCarbonoDetectado = gasDetected == LOW;
  if (monoxidoCarbonoDetectado) {
    Serial.println("⚠️ Monóxido de carbono detectado!");
  } else {
    Serial.println("✅ Sem detecção de gás.");
  }
  #pragma endregion
 
  enviarLeituraMQTT(averageGas, ppm, chamaDetectada, temperature, humidity, monoxidoCarbonoDetectado);
  delay(MS_TEMPO_LEITURA);
}