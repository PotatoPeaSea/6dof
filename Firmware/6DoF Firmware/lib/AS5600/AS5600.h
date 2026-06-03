#pragma once

#include <Arduino.h>
#include <Wire.h>

// ---------------------------------------------------------------------------
// AS5600 magnetic encoder — I2C driver.
//
// One As5600 instance describes one physical sensor: its bus, address, and
// pins, plus the most recent readings. The AS5600's I2C interface exposes the
// full register map (raw/filtered angle, magnitude, AGC, status), which is why
// it is preferred over the analog/PWM outputs for diagnostics.
//
// NOTE: every AS5600 shares the fixed I2C address 0x36, so multiple sensors
// cannot share one bus without a mux (e.g. TCA9548A). Give each its own
// TwoWire peripheral. I2C uses SDA (data) and SCL (clock) — there is no
// "SCLK".
// ---------------------------------------------------------------------------
class As5600 {
 public:
  // Default 7-bit I2C address (fixed in silicon).
  static constexpr uint8_t kDefaultAddress = 0x36;

  As5600(const char *name, TwoWire &bus, int pinSda, int pinScl,
         uint8_t address = kDefaultAddress);

  // Start the bus and report the initial magnet status over Serial.
  void begin(uint32_t clockHz = 400000);

  // Refresh all cached fields from the device. Returns true when a valid
  // angle was read (magnet detected and bus healthy).
  bool update();

  // Print the current reading to Serial.
  void print() const;

  // Accessors — valid only when ok() is true.
  bool     ok() const { return ok_; }
  bool     magnetDetected() const { return magnetDetected_; }
  uint8_t  status() const { return status_; }
  uint16_t angle() const { return angle_; }
  uint16_t rawAngle() const { return rawAngle_; }
  uint16_t magnitude() const { return magnitude_; }
  uint8_t  agc() const { return agc_; }
  float    angleDeg() const { return angle_ * kCountsToDeg; }
  const char *name() const { return name_; }

 private:
  // 4096 counts (12-bit) over a full revolution.
  static constexpr float kCountsToDeg = 360.0f / 4096.0f;

  // Register map.
  static constexpr uint8_t kRegStatus    = 0x0B; // MD/ML/MH diagnostic flags
  static constexpr uint8_t kRegRawAngle  = 0x0C; // 0x0C..0x0D unfiltered
  static constexpr uint8_t kRegAngle     = 0x0E; // 0x0E..0x0F filtered
  static constexpr uint8_t kRegAgc       = 0x1A; // 8-bit AGC gain
  static constexpr uint8_t kRegMagnitude = 0x1B; // 0x1B..0x1C field magnitude

  // STATUS register flag bits.
  static constexpr uint8_t kStatusMh = 1 << 3; // magnet too strong
  static constexpr uint8_t kStatusMl = 1 << 4; // magnet too weak
  static constexpr uint8_t kStatusMd = 1 << 5; // magnet detected

  // Low-level register access.
  bool readReg8(uint8_t reg, uint8_t &value);
  bool readReg12(uint8_t reg, uint16_t &value);

  // Config.
  const char *name_;
  TwoWire    *bus_;
  uint8_t     address_;
  int         pinSda_;
  int         pinScl_;

  // Cached readings.
  uint8_t  status_ = 0;
  uint16_t rawAngle_ = 0;
  uint16_t angle_ = 0;
  uint16_t magnitude_ = 0;
  uint8_t  agc_ = 0;
  bool     magnetDetected_ = false;
  bool     ok_ = false;
};
