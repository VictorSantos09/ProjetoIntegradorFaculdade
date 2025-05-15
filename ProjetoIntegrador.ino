#include <DHT.h>
#include <math.h>
 
#define DHTPIN D4
#define DHTTYPE DHT11
DHT dht(DHTPIN, DHTTYPE);
 
const int PIN_PLACALED = D4;
const int PIN_SENSOR_MQ7 = D5;
const int MS_TEMPO_LEITURA = 1000;
 
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
 
void setup() {
  Serial.begin(115200);
  dht.begin();
 
  pinMode(PIN_PLACALED, OUTPUT);
  pinMode(PIN_KY_026, INPUT);
  pinMode(PIN_SENSOR_MQ7, INPUT);
 
  calibrateSensor(); // MQ analógico
}
 
void loop() {
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
  if (flameDetected == LOW) {
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
  if (gasDetected == LOW) {
    Serial.println("⚠️ Monóxido de carbono detectado!");
  } else {
    Serial.println("✅ Sem detecção de gás.");
  }
  #pragma endregion
 
  delay(MS_TEMPO_LEITURA);
}