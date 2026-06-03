#include <Arduino.h>
#include <Wire.h>

#include "AS5600.h"

// ---------------------------------------------------------------------------
// 6DOF arm encoder readout.
//
// Each AS5600 shares the fixed I2C address 0x36, so every sensor gets its own
// hardware I2C bus. The ESP32-S3 has two. For more encoders, add a TCA9548A
// mux or switch to the address-programmable AS5600L.
// I2C uses SDA (data) and SCL (clock) — sensor 0 is SDA=GPIO1, SCL=GPIO2.
// ---------------------------------------------------------------------------
TwoWire I2CbusA = TwoWire(0);
TwoWire I2CbusB = TwoWire(1);

As5600 sensors[] = {
  As5600("joint0", I2CbusA, /*sda=*/1, /*scl=*/2),
  As5600("joint1", I2CbusB, /*sda=*/4, /*scl=*/5),
};
static constexpr size_t kSensorCount = sizeof(sensors) / sizeof(sensors[0]);

void setup() {
  Serial.begin(115200);
  while (!Serial) {
    ; // wait for USB CDC to come up on the ESP32-S3
  }

  for (auto &sensor : sensors) {
    sensor.begin();
  }
}

void loop() {
  for (auto &sensor : sensors) {
    sensor.update();
    sensor.print();
  }
  Serial.println();
  delay(100);
}
