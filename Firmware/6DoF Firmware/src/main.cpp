#include <Arduino.h>
#include <Wire.h>

#include "AS5600.h"

// ---------------------------------------------------------------------------
// 6DOF arm encoder readout.
//
// Each AS5600 shares the fixed I2C address 0x36, so every sensor gets its own
// hardware I2C bus. The ESP32-S3 has two, exposed by the Arduino core as the
// pre-built `Wire` (port 0) and `Wire1` (port 1) globals. Use those directly —
// constructing extra TwoWire(0)/TwoWire(1) objects double-claims the same
// hardware port and leaves the controller in ESP_ERR_INVALID_STATE.
// For more encoders, add a TCA9548A mux or the address-programmable AS5600L.
// I2C uses SDA (data) and SCL (clock) — sensor 0 is SDA=GPIO1, SCL=GPIO2.
// ---------------------------------------------------------------------------
As5600 sensors[] = {
  As5600("joint0", Wire,  /*sda=*/1, /*scl=*/42),
  As5600("joint1", Wire1, /*sda=*/4, /*scl=*/5),
};
static constexpr size_t kSensorCount = sizeof(sensors) / sizeof(sensors[0]);

// Probe every 7-bit address on a bus and report which ones ACK. A healthy
// AS5600 shows up at 0x36; an empty result points at wiring, power, missing
// pull-ups, or the wrong pins rather than a sensor/firmware bug.
static void scanBus(TwoWire &bus, const char *label) {
  Serial.printf("Scanning %s ...\n", label);
  uint8_t found = 0;
  for (uint8_t addr = 0x08; addr < 0x78; ++addr) {
    bus.beginTransmission(addr);
    if (bus.endTransmission(true) == 0) {
      Serial.printf("  device at 0x%02X\n", addr);
      ++found;
    }
  }
  if (found == 0) {
    Serial.printf("  no devices — check SDA/SCL pins, 3V3, GND, pull-ups.\n");
  }
}

void setup() {
  Serial.begin(115200);
  while (!Serial) {
    ; // wait for USB CDC to come up on the ESP32-S3
  }

  for (auto &sensor : sensors) {
    sensor.begin(); // starts the sensor's bus
  }

  scanBus(Wire,  "Wire  (joint0, GPIO1/2)");
  scanBus(Wire1, "Wire1 (joint1, GPIO4/5)");
}

void loop() {
  for (auto &sensor : sensors) {
    sensor.update();
    sensor.print();
  }
  Serial.println();
  delay(1000); // print once per second
}
