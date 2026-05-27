# Arm ↔ Host Serial Protocol

Status: **draft v0.1**. The arm firmware does not yet exist; the simulator's `KeyboardIronInput` is the V1 source. This document locks the wire format the firmware must produce so `SerialIronInput` can be implemented without renegotiation.

## Transport

- **Layer:** USB CDC ACM (the MCU enumerates as a virtual COM port).
- **Baud:** 115200, 8N1, no flow control. Baud is ignored over USB CDC but the host opens with these settings for cross-platform behavior.
- **Framing:** newline-terminated ASCII (`\n`, single byte `0x0A`). No `\r`. Lines longer than 256 bytes are dropped.
- **Encoding:** UTF-8 ASCII subset. No locale-dependent decimal commas — always `.`.

## Messages

Every line starts with a single-character message type, followed by comma-separated fields, then `\n`.

### `P` — pose sample (arm → host)

```
P,<x_mm>,<y_mm>,<z_mm>,<pitch_deg>,<yaw_deg>,<roll_deg>,<tip_temp_c>,<seq>\n
```

| Field        | Type    | Units    | Notes                                              |
|--------------|---------|----------|----------------------------------------------------|
| `x_mm`       | float   | mm       | Tip position in arm base frame.                    |
| `y_mm`       | float   | mm       | "                                                  |
| `z_mm`       | float   | mm       | "                                                  |
| `pitch_deg`  | float   | deg      | Intrinsic rotation about tip frame X.              |
| `yaw_deg`    | float   | deg      | Intrinsic rotation about tip frame Y.              |
| `roll_deg`   | float   | deg      | Intrinsic rotation about tip frame Z.              |
| `tip_temp_c` | float   | °C       | Measured tip temperature; `nan` if not yet probed. |
| `seq`        | uint16  | —        | Monotonic counter mod 65536. Host detects drops.   |

Floats use up to 4 decimal places. Target update rate: **1 kHz**, hard floor 200 Hz. Host buffers and consumes the latest sample each FixedUpdate (5 ms).

### `S` — status (arm → host, on connect and on request)

```
S,<fw_version>,<hw_rev>,<uptime_ms>\n
```

Sent unsolicited within 100 ms of CDC enumeration and in response to `?\n`.

### `?` — status request (host → arm)

```
?\n
```

### `OK` / `ERR` — acknowledgement (arm → host)

```
OK\n
ERR,<code>,<message>\n
```

`ERR` is fire-and-forget; the host logs it. `code` is a short token (`PARSE`, `RANGE`, `BUSY`).

## Host behavior

- On port open, host waits up to 500 ms for an `S` line. If none arrives, it sends `?\n` and retries up to 3 times. Failing that, port is closed and the user sees a connection error in the HUD.
- Host treats any malformed line as a soft error: increment a counter, drop the line, continue. Three consecutive parse errors within one second triggers a reconnect.
- Host never sends pose data — pose is one-way arm → host.

## Open questions (resolve before firmware ships)

- Add a CRC field to `P`? Probably yes for 1 kHz over long cables; deferred until firmware exists.
- Coordinate frame transform between arm base and Unity world is host-side configuration (`Software/Assets/Settings/ArmCalibration.asset`), not in this protocol.
- Heater command path (host → arm) for closed-loop tip temperature: not in V1. When added, define an `H,<setpoint_c>\n` message and bump protocol version.
