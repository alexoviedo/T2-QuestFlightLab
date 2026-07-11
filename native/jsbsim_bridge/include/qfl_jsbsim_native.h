#ifndef QFL_JSBSIM_NATIVE_H
#define QFL_JSBSIM_NATIVE_H

#if defined(_WIN32)
#  if defined(QFL_JSBSIM_NATIVE_BUILD)
#    define QFL_JSBSIM_API __declspec(dllexport)
#  else
#    define QFL_JSBSIM_API __declspec(dllimport)
#  endif
#else
#  define QFL_JSBSIM_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef void* qfl_jsbsim_handle;

typedef struct qfl_jsbsim_initial_conditions {
  double latitude_degrees;
  double longitude_degrees;
  double altitude_msl_feet;
  double terrain_elevation_msl_feet;
  double calibrated_airspeed_knots;
  double heading_degrees;
  double pitch_degrees;
  double bank_degrees;
  double flight_path_angle_degrees;
  int engine_running;
} qfl_jsbsim_initial_conditions;

typedef struct qfl_jsbsim_controls {
  double aileron;
  double elevator;
  double rudder;
  double throttle;
  double mixture;
  double carb_heat;
  double elevator_trim;
  double flaps;
  double left_brake;
  double right_brake;
} qfl_jsbsim_controls;

typedef struct qfl_jsbsim_atmosphere {
  double wind_north_feet_per_second;
  double wind_east_feet_per_second;
  double wind_down_feet_per_second;
} qfl_jsbsim_atmosphere;

typedef struct qfl_jsbsim_state {
  double simulation_time_seconds;
  double latitude_degrees;
  double longitude_degrees;
  double altitude_msl_feet;
  double altitude_agl_feet;
  double calibrated_airspeed_knots;
  double ground_speed_knots;
  double vertical_speed_feet_per_minute;
  double heading_degrees;
  double pitch_degrees;
  double bank_degrees;
  double angle_of_attack_degrees;
  double sideslip_degrees;
  double velocity_north_feet_per_second;
  double velocity_east_feet_per_second;
  double velocity_down_feet_per_second;
  double roll_rate_radians_per_second;
  double pitch_rate_radians_per_second;
  double yaw_rate_radians_per_second;
  double engine_rpm;
  double load_factor_g;
  int weight_on_wheels;
} qfl_jsbsim_state;

QFL_JSBSIM_API int qfl_jsbsim_api_version(void);
QFL_JSBSIM_API qfl_jsbsim_handle qfl_jsbsim_create(void);
QFL_JSBSIM_API void qfl_jsbsim_destroy(qfl_jsbsim_handle handle);
QFL_JSBSIM_API int qfl_jsbsim_load_aircraft(
    qfl_jsbsim_handle handle,
    const char* data_root_utf8,
    const char* aircraft_name_utf8);
QFL_JSBSIM_API int qfl_jsbsim_reset(
    qfl_jsbsim_handle handle,
    const qfl_jsbsim_initial_conditions* initial_conditions);
QFL_JSBSIM_API int qfl_jsbsim_set_controls(
    qfl_jsbsim_handle handle,
    const qfl_jsbsim_controls* controls);
QFL_JSBSIM_API int qfl_jsbsim_set_atmosphere(
    qfl_jsbsim_handle handle,
    const qfl_jsbsim_atmosphere* atmosphere);
QFL_JSBSIM_API int qfl_jsbsim_advance(qfl_jsbsim_handle handle, double fixed_delta_time_seconds);
QFL_JSBSIM_API int qfl_jsbsim_get_state(qfl_jsbsim_handle handle, qfl_jsbsim_state* state);
QFL_JSBSIM_API const char* qfl_jsbsim_last_error(qfl_jsbsim_handle handle);

#ifdef __cplusplus
}
#endif

#endif
