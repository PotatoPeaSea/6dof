#include "AS5600.h"

As5600::As5600(const char *name, TwoWire &bus, int pinSda, int pinScl,
               uint8_t address)
    : name_(name),
      bus_(&bus),
      address_(address),
      pinSda_(pinSda),
      pinScl_(pinScl) {}

bool As5600::readReg8(uint8_t reg, uint8_t &value) {
  bus_->beginTransmission(address_);
  bus_->write(reg);
  if (bus_->endTransmission(true) != 0) { // send stop; ng driver dislikes
                                          // the repeated-start combo
    return false;
  }
  if (bus_->requestFrom(address_, (uint8_t)1) != 1) {
    return false;
  }
  value = bus_->read();
  return true;
}

// Read a 12-bit value split across two consecutive registers (high byte at
// the lower address holds bits [11:8] in its low nibble; low byte holds
// bits [7:0]). Both bytes are pulled in one transaction so they come from the
// same conversion cycle.
bool As5600::readReg12(uint8_t reg, uint16_t &value) {
  bus_->beginTransmission(address_);
  bus_->write(reg);
  if (bus_->endTransmission(true) != 0) { // send stop (see readReg8)
    return false;
  }
  if (bus_->requestFrom(address_, (uint8_t)2) != 2) {
    return false;
  }
  uint8_t hi = bus_->read();
  uint8_t lo = bus_->read();
  value = ((uint16_t)(hi & 0x0F) << 8) | lo; // assemble 12-bit result
  return true;
}

void As5600::begin(uint32_t clockHz) {
  if (!bus_->begin(pinSda_, pinScl_, clockHz)) {
    Serial.printf("[%s] I2C bus begin() failed on SDA=%d SCL=%d.\n", name_,
                  pinSda_, pinScl_);
    return;
  }

  // Per the datasheet, confirm a magnet is present before trusting any angle.
  // MD must be 1; reading ANGLE without it risks latching a garbage value.
  uint8_t status = 0;
  if (!readReg8(kRegStatus, status)) {
    Serial.printf("[%s] STATUS read failed — check wiring/address.\n", name_);
    return;
  }
  Serial.printf("[%s] STATUS 0x%02X  MD=%d ML=%d MH=%d\n", name_, status,
                (status & kStatusMd) ? 1 : 0, (status & kStatusMl) ? 1 : 0,
                (status & kStatusMh) ? 1 : 0);
  if (!(status & kStatusMd)) {
    Serial.printf("[%s] no magnet detected (MD=0) — readings invalid.\n",
                  name_);
  }
}

bool As5600::update() {
  ok_ = false;
  if (!readReg8(kRegStatus, status_)) {
    return false;
  }
  magnetDetected_ = (status_ & kStatusMd) != 0;
  if (!magnetDetected_) {
    return false; // don't store a stale/invalid angle as valid
  }

  if (!readReg12(kRegAngle, angle_)) {
    return false;
  }
  // Diagnostics — useful for magnet alignment/health, not required for angle.
  readReg12(kRegRawAngle, rawAngle_);
  readReg12(kRegMagnitude, magnitude_);
  readReg8(kRegAgc, agc_);

  ok_ = true;
  return true;
}

void As5600::print() const {
  if (!ok_) {
    Serial.printf("[%s] magnet not detected — angle skipped.\n", name_);
    return;
  }
  Serial.printf("[%s] %4u counts  %7.2f deg   (mag=%u agc=%u)\n", name_,
                angle_, angle_ * kCountsToDeg, magnitude_, agc_);
}
