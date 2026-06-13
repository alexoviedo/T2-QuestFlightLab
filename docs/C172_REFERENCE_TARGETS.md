# C172-Style Reference Targets

QuestFlightLab uses these values as prototype targets for a C172S-style powered trainer direction. They are not an aircraft flight manual, not a certified simulator basis, and not a claim of final C172 fidelity.

## Sources

- Textron Aviation, Cessna Skyhawk specifications: https://cessna.txtav.com/en/piston/cessna-skyhawk
- FAA Dynamic Regulatory System, Type Certificate Data Sheet 3A12 for Cessna 172 series context: https://drs.faa.gov/browse/excelExternalWindow/DRSDOCID153801600120250627164731.0001%3FmodalOpened%3Dtrue?modalOpened=true
- Purdue Aviation C172SP data sheet for V-speed/training reference values: https://www.purdueaviationllc.com/storage/app/media/Data%20Sheets/C172SP%20Data%20Sheet.pdf

## Units

- Airspeed: KIAS or knots unless noted.
- Altitude: feet.
- Vertical speed: feet per minute.
- Mass/weight: pounds for source references, kg for Unity physics.
- Attitude/control: degrees or normalized -1..1 controls.

## Reference Targets

| Target | v0.3 value | Source basis | Implementation note |
| --- | ---: | --- | --- |
| Max gross / takeoff weight | 2,550 lb / 1,157 kg | Textron and C172SP data sheet | `C172ReferenceSpeeds.maxGrossWeightKg` drives runtime mass target. |
| Wing area | 174 sq ft / 16.17 m2 | Textron | Rounded to 16.2 m2 in config. |
| Wingspan | 36 ft 1 in / 11.0 m | Textron | Used for aspect-ratio documentation. |
| Engine power | 180 hp at 2700 RPM | Textron and C172SP data sheet | Power/thrust remains a placeholder. |
| Clean stall, Vs1 | 48 KIAS | C172SP data sheet | Used for warning/low-speed target. |
| Landing stall, Vso | 40 KIAS | C172SP data sheet | Blended by flap setting. |
| Rotation, Vr | 55 KIAS | C172SP data sheet | Takeoff scenario targets acceleration through Vr. |
| Vx | 62 KIAS | C172SP data sheet | Reference only in v0.3. |
| Vy | 74 KIAS | C172SP data sheet | Vy climb scenario targets this band. |
| Best glide | 68 KIAS | C172SP data sheet | Used as non-climb reference speed placeholder. |
| Vfe 0-10 deg | 110 KIAS | C172SP data sheet | Documented; v0.3 only uses max flap-extension guard loosely. |
| Vfe 10-30 deg | 85 KIAS | C172SP data sheet | Config target. |
| Vno | 129 KIAS | C172SP data sheet | Documented, not enforced yet. |
| Vne | 163 KIAS | Textron and C172SP data sheet | Config target. |
| Enroute climb | 75-85 KIAS | C172SP data sheet | Vy scenario allows a tolerance around 74-84 kt. |
| Approach | 60-70 KIAS | C172SP data sheet | Reference only in v0.3. |
| Takeoff ground roll | 960 ft / 293 m | Textron | Stored as training target; runway roll remains simplified. |
| Maximum climb rate | 730 fpm | Textron | v0.3 targets positive climb, not exact rate. |

## Training Behavior Targets

- Before takeoff: verify controls, trim, flaps, mixture, carb heat, engine instruments, doors/windows, runway heading/departure brief placeholders.
- Normal takeoff: line up, smoothly apply throttle, maintain centerline, rotate near Vr, climb toward Vy, maintain runway heading, and perform after-takeoff cleanup placeholder.
- Turns: shallow turns should show bank and heading change without excessive pitch/bank.
- Stall warning: slow-flight/stall-onset scenarios should record warning before a deep stall.
- Stall recovery: reduce AoA, add power, retract flaps placeholder, recover positive climb without pretending to model a certified stall recovery.

## Approximation Boundary

These are implementation targets for a deterministic simulator harness. They do not replace a POH, flight instructor, certified training device, or validated aircraft model.
