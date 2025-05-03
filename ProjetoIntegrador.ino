#include <DHT.h>

#define DHTPIN D4         // Pino de leitura do DHT11
#define DHTTYPE DHT11     // Definir como DHT11

DHT dht(DHTPIN, DHTTYPE);  // Inicializa o sensor DHT11

const int PIN_PLACALED = D4;          // LED da placa
const int PIN_SENSOR_MQ7 = D5;  // Pino digital (DO do MQ-7)
const int MS_TEMPO_LEITURA = 1000;

#pragma region [VARIAVEIS KY-026]
const int PIN_KY_026 = D3;            // DO do KY-026
#pragma endregion

void setup() {
  Serial.begin(115200);   // Inicializa a comunicação serial
  dht.begin();            // Inicializa o sensor DHT11

  pinMode(PIN_PLACALED, OUTPUT);
  pinMode(PIN_KY_026, INPUT);
  pinMode(PIN_SENSOR_MQ7, INPUT);  // Define o pino como entrada digital
}

void loop() {
  #pragma region [PISCAR PLACA]
  digitalWrite(PIN_PLACALED, HIGH);
  delay(300);
  digitalWrite(PIN_PLACALED, LOW);
  #pragma endregion

  #pragma region [LEITURA MQ-2]
  int gasValue = analogRead(A0);
  Serial.print("⚠️Gás detectado!: ");
  Serial.println(gasValue);
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
  float humidity = dht.readHumidity();    // Lê a umidade
  float temperature = dht.readTemperature(); // Lê a temperatura (em Celsius)

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

  // Se a leitura for LOW, significa que o gás foi detectado
  if (gasDetected == LOW) {
    Serial.println("⚠️ Monóxido de carbono detectado!");
  } else {
    Serial.println("✅ Sem detecção de gás.");
  }
  #pragma endregion

  delay(MS_TEMPO_LEITURA);
}
