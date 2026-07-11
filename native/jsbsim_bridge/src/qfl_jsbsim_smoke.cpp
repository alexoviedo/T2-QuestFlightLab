#include "qfl_jsbsim_native.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdlib>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <vector>

int main(int argc, char** argv) {
  if (argc != 2) {
    std::cerr << "usage: qfl_jsbsim_smoke <jsbsim-data-root>\n";
    return 2;
  }

  qfl_jsbsim_handle handle = qfl_jsbsim_create();
  if (!handle) {
    std::cerr << qfl_jsbsim_last_error(nullptr) << "\n";
    return 3;
  }

  if (!qfl_jsbsim_load_aircraft(handle, argv[1], "c172x")) {
    std::cerr << qfl_jsbsim_last_error(handle) << "\n";
    qfl_jsbsim_destroy(handle);
    return 4;
  }

  qfl_jsbsim_initial_conditions initial{};
  initial.latitude_degrees = 40.03936527;
  initial.longitude_degrees = -105.22608958;
  initial.altitude_msl_feet = 5292.1;
  initial.terrain_elevation_msl_feet = 5288.0;
  initial.heading_degrees = 89.972;
  initial.engine_running = 1;
  if (!qfl_jsbsim_reset(handle, &initial)) {
    std::cerr << qfl_jsbsim_last_error(handle) << "\n";
    qfl_jsbsim_destroy(handle);
    return 5;
  }

  qfl_jsbsim_atmosphere atmosphere{};
  qfl_jsbsim_set_atmosphere(handle, &atmosphere);
  qfl_jsbsim_controls controls{};
  controls.mixture = 1.0;
  controls.left_brake = 1.0;
  controls.right_brake = 1.0;
  constexpr double dt = 1.0 / 120.0;
  constexpr int steps = 7200;
  std::vector<double> timings;
  timings.reserve(steps - 240);
  qfl_jsbsim_state state{};

  for (int i = 0; i < steps; ++i) {
    const double t = i * dt;
    if (t >= 2.0) {
      controls.left_brake = 0.0;
      controls.right_brake = 0.0;
      controls.throttle = 1.0;
    }
    if (t >= 24.0) controls.elevator = 0.18;
    if (t >= 38.0) controls.elevator = 0.05;
    qfl_jsbsim_set_controls(handle, &controls);
    const auto start = std::chrono::steady_clock::now();
    if (!qfl_jsbsim_advance(handle, dt)) {
      std::cerr << qfl_jsbsim_last_error(handle) << "\n";
      qfl_jsbsim_destroy(handle);
      return 6;
    }
    const auto end = std::chrono::steady_clock::now();
    if (i >= 240) {
      timings.push_back(std::chrono::duration<double, std::milli>(end - start).count());
    }
  }

  if (!qfl_jsbsim_get_state(handle, &state)) {
    std::cerr << qfl_jsbsim_last_error(handle) << "\n";
    qfl_jsbsim_destroy(handle);
    return 7;
  }
  qfl_jsbsim_destroy(handle);

  std::sort(timings.begin(), timings.end());
  const double average = std::accumulate(timings.begin(), timings.end(), 0.0) / timings.size();
  const double p95 = timings[static_cast<std::size_t>(std::floor((timings.size() - 1) * 0.95))];
  const bool finite = std::isfinite(state.latitude_degrees) &&
                      std::isfinite(state.longitude_degrees) &&
                      std::isfinite(state.altitude_msl_feet) &&
                      std::isfinite(state.calibrated_airspeed_knots);
  std::cout << std::fixed << std::setprecision(6)
            << "{\"status\":\"" << (finite ? "PASS" : "FAIL")
            << "\",\"api_version\":" << qfl_jsbsim_api_version()
            << ",\"steps\":" << steps
            << ",\"average_step_ms\":" << average
            << ",\"p95_step_ms\":" << p95
            << ",\"final_airspeed_kt\":" << state.calibrated_airspeed_knots
            << ",\"final_agl_ft\":" << state.altitude_agl_feet
            << ",\"final_heading_deg\":" << state.heading_degrees
            << "}\n";
  return finite ? 0 : 8;
}
