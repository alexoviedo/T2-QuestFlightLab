# USB2BLE Integration

## Primary Persona

Use USB2BLE's Xbox BLE-compatible persona first:

- Persona: `xbox_wireless_controller`
- Bluetooth name: `Xbox Wireless Controller`
- Witnessed Windows HID identity: `045e:0b13`
- Recommended identity strategy for this Quest test: `persona_static_random_experimental`

Do not use Web Serial inside Quest. Web Serial remains a desktop configuration path.

## Practical Flight Pack Mapping

The v0.1 app maps Unity Input System `Gamepad` controls as:

| USB2BLE Xbox target | Unity Gamepad | Aircraft control |
| --- | --- | --- |
| `left_x` | left stick X | aileron / roll |
| `left_y` | left stick Y | elevator / pitch, inverted by default for flight feel |
| `right_x` | right stick X | rudder / yaw |
| `left_trigger` | left trigger | left toe brake |
| `right_trigger` | right trigger | right toe brake |
| A | south button | evidence marker / acknowledge |
| B | east button | reset scenario |
| X/Y | west/north buttons | flaps down/up placeholder |
| D-pad | dpad | trim/test marker placeholder |

The USB2BLE Xbox practical profile intentionally leaves TWCS throttle unmapped because the Xbox trigger slots are used for toe brakes.

