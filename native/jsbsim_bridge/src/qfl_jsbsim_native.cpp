#include "qfl_jsbsim_native.h"

#include <FGFDMExec.h>
#include <simgear/misc/sg_path.hxx>

#include <algorithm>
#include <cmath>
#include <memory>
#include <new>
#include <string>

namespace {

constexpr int kApiVersion = 1;
constexpr double kFeetPerSecondToKnots = 1.0 / 1.6878098571011957;
thread_local std::string g_last_error;

struct NativeInstance {
  std::unique_ptr<JSBSim::FGFDMExec> fdm;
  std::string data_root;
  std::string aircraft_name;
  std::string last_error;
  bool loaded = false;
};

NativeInstance* instance(qfl_jsbsim_handle handle) {
  return reinterpret_cast<NativeInstance*>(handle);
}

double clamp(double value, double minimum, double maximum) {
  return std::max(minimum, std::min(maximum, value));
}

void set_error(NativeInstance* native, const std::string& message) {
  g_last_error = message;
  if (native) native->last_error = message;
}

bool require_loaded(NativeInstance* native) {
  if (native && native->loaded && native->fdm) return true;
  set_error(native, "JSBSim instance is null or has no loaded aircraft.");
  return false;
}

void set_property(JSBSim::FGFDMExec& fdm, const char* name, double value) {
  fdm.SetPropertyValue(name, value);
}

double get_property(JSBSim::FGFDMExec& fdm, const char* name) {
  return fdm.GetPropertyValue(name);
}

int catch_failure(NativeInstance* native, const char* operation) {
  try {
    throw;
  } catch (const std::exception& exception) {
    set_error(native, std::string(operation) + ": " + exception.what());
  } catch (...) {
    set_error(native, std::string(operation) + ": unknown native exception");
  }
  return 0;
}

}  // namespace

extern "C" {

int qfl_jsbsim_api_version(void) {
  return kApiVersion;
}

qfl_jsbsim_handle qfl_jsbsim_create(void) {
  try {
    auto native = std::make_unique<NativeInstance>();
    g_last_error.clear();
    return native.release();
  } catch (...) {
    catch_failure(nullptr, "create");
    return nullptr;
  }
}

void qfl_jsbsim_destroy(qfl_jsbsim_handle handle) {
  delete instance(handle);
}

int qfl_jsbsim_load_aircraft(
    qfl_jsbsim_handle handle,
    const char* data_root_utf8,
    const char* aircraft_name_utf8) {
  NativeInstance* native = instance(handle);
  if (!native || !data_root_utf8 || !aircraft_name_utf8) {
    set_error(native, "load_aircraft requires a valid instance, data root, and aircraft name.");
    return 0;
  }

  try {
    native->fdm = std::make_unique<JSBSim::FGFDMExec>();
    native->fdm->SetDebugLevel(0);
    native->data_root = data_root_utf8;
    native->aircraft_name = aircraft_name_utf8;
    native->fdm->SetRootDir(SGPath(native->data_root));
    native->fdm->SetAircraftPath(SGPath("aircraft"));
    native->fdm->SetEnginePath(SGPath("engine"));
    native->fdm->SetSystemsPath(SGPath("systems"));
    if (!native->fdm->LoadModel(native->aircraft_name)) {
      set_error(native, "JSBSim could not load aircraft '" + native->aircraft_name + "' from '" + native->data_root + "'.");
      native->fdm.reset();
      return 0;
    }
    native->fdm->DisableOutput();
    native->fdm->DisableInput();
    native->loaded = true;
    native->last_error.clear();
    return 1;
  } catch (...) {
    native->fdm.reset();
    native->loaded = false;
    return catch_failure(native, "load_aircraft");
  }
}

int qfl_jsbsim_reset(
    qfl_jsbsim_handle handle,
    const qfl_jsbsim_initial_conditions* initial_conditions) {
  NativeInstance* native = instance(handle);
  if (!require_loaded(native) || !initial_conditions) {
    if (native && !initial_conditions) set_error(native, "reset requires initial conditions.");
    return 0;
  }

  try {
    JSBSim::FGFDMExec& fdm = *native->fdm;
    fdm.Setsim_time(0.0);
    // A single native instance is deliberately reused by the validation and
    // runtime reset paths. Clear every command that can otherwise leak from a
    // previous flight before RunIC initializes actuator and ground-reaction
    // state. The first external control sample replaces these idle values.
    set_property(fdm, "ap/elevator_cmd", 0.0);
    set_property(fdm, "ap/aileron_cmd", 0.0);
    set_property(fdm, "ap/roll-cmd-norm-output", 0.0);
    set_property(fdm, "fcs/aileron-cmd-norm", 0.0);
    set_property(fdm, "fcs/elevator-cmd-norm", 0.0);
    set_property(fdm, "fcs/rudder-cmd-norm", 0.0);
    set_property(fdm, "fcs/pitch-trim-cmd-norm", 0.0);
    set_property(fdm, "fcs/roll-trim-cmd-norm", 0.0);
    set_property(fdm, "fcs/yaw-trim-cmd-norm", 0.0);
    set_property(fdm, "fcs/flap-cmd-norm", 0.0);
    set_property(fdm, "fcs/left-brake-cmd-norm", 0.0);
    set_property(fdm, "fcs/right-brake-cmd-norm", 0.0);
    set_property(fdm, "fcs/center-brake-cmd-norm", 0.0);
    set_property(fdm, "fcs/throttle-cmd-norm", initial_conditions->engine_running ? 0.10 : 0.0);
    set_property(fdm, "fcs/throttle-cmd-norm[0]", initial_conditions->engine_running ? 0.10 : 0.0);
    set_property(fdm, "fcs/mixture-cmd-norm", initial_conditions->engine_running ? 1.0 : 0.0);
    set_property(fdm, "fcs/mixture-cmd-norm[0]", initial_conditions->engine_running ? 1.0 : 0.0);
    set_property(fdm, "propulsion/active_engine", 0.0);
    set_property(fdm, "propulsion/magneto_cmd", initial_conditions->engine_running ? 3.0 : 0.0);
    set_property(fdm, "propulsion/starter_cmd", 0.0);
    // JSBSim's supported direct-start property is set before RunIC so the
    // initial propulsion forces and moments belong to the requested state.
    set_property(fdm, "propulsion/set-running", initial_conditions->engine_running ? -1.0 : 0.0);
    set_property(fdm, "ic/lat-geod-deg", initial_conditions->latitude_degrees);
    set_property(fdm, "ic/long-gc-deg", initial_conditions->longitude_degrees);
    set_property(fdm, "ic/h-sl-ft", initial_conditions->altitude_msl_feet);
    set_property(fdm, "ic/terrain-elevation-ft", initial_conditions->terrain_elevation_msl_feet);
    set_property(fdm, "ic/vc-kts", std::max(0.0, initial_conditions->calibrated_airspeed_knots));
    set_property(fdm, "ic/gamma-deg", initial_conditions->flight_path_angle_degrees);
    set_property(fdm, "ic/psi-true-deg", initial_conditions->heading_degrees);
    set_property(fdm, "ic/theta-deg", initial_conditions->pitch_degrees);
    set_property(fdm, "ic/phi-deg", initial_conditions->bank_degrees);
    if (!fdm.RunIC()) {
      set_error(native, "JSBSim RunIC failed.");
      return 0;
    }
    native->last_error.clear();
    return 1;
  } catch (...) {
    return catch_failure(native, "reset");
  }
}

int qfl_jsbsim_set_controls(
    qfl_jsbsim_handle handle,
    const qfl_jsbsim_controls* controls) {
  NativeInstance* native = instance(handle);
  if (!require_loaded(native) || !controls) {
    if (native && !controls) set_error(native, "set_controls requires a control struct.");
    return 0;
  }

  try {
    JSBSim::FGFDMExec& fdm = *native->fdm;
    const double throttle = clamp(controls->throttle, 0.0, 1.0);
    const double mixture = clamp(controls->mixture, 0.0, 1.0);
    set_property(fdm, "fcs/aileron-cmd-norm", clamp(controls->aileron, -1.0, 1.0));
    set_property(fdm, "fcs/elevator-cmd-norm", clamp(controls->elevator, -1.0, 1.0));
    set_property(fdm, "fcs/rudder-cmd-norm", clamp(controls->rudder, -1.0, 1.0));
    set_property(fdm, "fcs/throttle-cmd-norm", throttle);
    set_property(fdm, "fcs/throttle-cmd-norm[0]", throttle);
    set_property(fdm, "fcs/mixture-cmd-norm", mixture);
    set_property(fdm, "fcs/mixture-cmd-norm[0]", mixture);
    // FGFCS and the pinned c172x model bind the longitudinal trim input under
    // pitch-trim. "elevator-trim-cmd-norm" would create an unrelated dynamic
    // property and silently leave the aircraft untrimmed.
    set_property(fdm, "fcs/pitch-trim-cmd-norm", clamp(controls->elevator_trim, -1.0, 1.0));
    set_property(fdm, "fcs/flap-cmd-norm", clamp(controls->flaps, 0.0, 1.0));
    set_property(fdm, "fcs/left-brake-cmd-norm", clamp(controls->left_brake, 0.0, 1.0));
    set_property(fdm, "fcs/right-brake-cmd-norm", clamp(controls->right_brake, 0.0, 1.0));
    set_property(fdm, "fcs/center-brake-cmd-norm", clamp(std::max(controls->left_brake, controls->right_brake), 0.0, 1.0));
    set_property(fdm, "systems/carb-heat-norm", clamp(controls->carb_heat, 0.0, 1.0));
    return 1;
  } catch (...) {
    return catch_failure(native, "set_controls");
  }
}

int qfl_jsbsim_set_atmosphere(
    qfl_jsbsim_handle handle,
    const qfl_jsbsim_atmosphere* atmosphere) {
  NativeInstance* native = instance(handle);
  if (!require_loaded(native) || !atmosphere) {
    if (native && !atmosphere) set_error(native, "set_atmosphere requires an atmosphere struct.");
    return 0;
  }

  try {
    set_property(*native->fdm, "atmosphere/wind-north-fps", atmosphere->wind_north_feet_per_second);
    set_property(*native->fdm, "atmosphere/wind-east-fps", atmosphere->wind_east_feet_per_second);
    set_property(*native->fdm, "atmosphere/wind-down-fps", atmosphere->wind_down_feet_per_second);
    return 1;
  } catch (...) {
    return catch_failure(native, "set_atmosphere");
  }
}

int qfl_jsbsim_advance(qfl_jsbsim_handle handle, double fixed_delta_time_seconds) {
  NativeInstance* native = instance(handle);
  if (!require_loaded(native)) return 0;
  if (!std::isfinite(fixed_delta_time_seconds) || fixed_delta_time_seconds <= 0.0) {
    set_error(native, "advance requires a finite positive timestep.");
    return 0;
  }

  try {
    native->fdm->Setdt(fixed_delta_time_seconds);
    if (!native->fdm->Run()) {
      set_error(native, "JSBSim Run returned false.");
      return 0;
    }
    return 1;
  } catch (...) {
    return catch_failure(native, "advance");
  }
}

int qfl_jsbsim_get_state(qfl_jsbsim_handle handle, qfl_jsbsim_state* state) {
  NativeInstance* native = instance(handle);
  if (!require_loaded(native) || !state) {
    if (native && !state) set_error(native, "get_state requires an output state struct.");
    return 0;
  }

  try {
    JSBSim::FGFDMExec& fdm = *native->fdm;
    state->simulation_time_seconds = fdm.GetSimTime();
    state->latitude_degrees = get_property(fdm, "position/lat-geod-deg");
    state->longitude_degrees = get_property(fdm, "position/long-gc-deg");
    state->altitude_msl_feet = get_property(fdm, "position/h-sl-ft");
    state->altitude_agl_feet = get_property(fdm, "position/h-agl-ft");
    state->calibrated_airspeed_knots = get_property(fdm, "velocities/vc-kts");
    state->ground_speed_knots = get_property(fdm, "velocities/vg-fps") * kFeetPerSecondToKnots;
    state->vertical_speed_feet_per_minute = get_property(fdm, "velocities/h-dot-fps") * 60.0;
    state->heading_degrees = get_property(fdm, "attitude/psi-deg");
    state->pitch_degrees = get_property(fdm, "attitude/theta-deg");
    state->bank_degrees = get_property(fdm, "attitude/phi-deg");
    state->angle_of_attack_degrees = get_property(fdm, "aero/alpha-deg");
    state->sideslip_degrees = get_property(fdm, "aero/beta-deg");
    state->velocity_north_feet_per_second = get_property(fdm, "velocities/v-north-fps");
    state->velocity_east_feet_per_second = get_property(fdm, "velocities/v-east-fps");
    state->velocity_down_feet_per_second = get_property(fdm, "velocities/v-down-fps");
    state->roll_rate_radians_per_second = get_property(fdm, "velocities/p-rad_sec");
    state->pitch_rate_radians_per_second = get_property(fdm, "velocities/q-rad_sec");
    state->yaw_rate_radians_per_second = get_property(fdm, "velocities/r-rad_sec");
    state->engine_rpm = get_property(fdm, "propulsion/engine/engine-rpm");
    state->load_factor_g = get_property(fdm, "forces/load-factor");
    state->weight_on_wheels = get_property(fdm, "gear/wow") > 0.5 ? 1 : 0;
    return 1;
  } catch (...) {
    return catch_failure(native, "get_state");
  }
}

const char* qfl_jsbsim_last_error(qfl_jsbsim_handle handle) {
  NativeInstance* native = instance(handle);
  return native ? native->last_error.c_str() : g_last_error.c_str();
}

}  // extern "C"
