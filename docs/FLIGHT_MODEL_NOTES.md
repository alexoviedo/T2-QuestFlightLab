# Flight Model Notes

v0.1 uses a deliberately simple C172-style approximation to make input, control surfaces, and scene motion testable on Quest.

Approximate seed constants:

- Mass: 1111 kg
- Wing area: 16.2 m2
- Stall speed clean: 48 kt
- Stall speed landing: 40 kt
- Cruise placeholder: 105 kt
- Rotation placeholder: 55 kt
- Best climb placeholder: 74 kt
- Never exceed placeholder: 163 kt

These values are placeholders for a structured prototype and are not a validated C172 model.

