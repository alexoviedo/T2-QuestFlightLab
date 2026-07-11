# JSBSim native runtime gate

This directory contains a project-owned compact C ABI around pinned JSBSim
`1.3.1`, revision `3b25f25e49b42d0489c04ac805674fc1450ca579`.

JSBSim is licensed under GNU LGPL 2.1 or later. The build keeps JSBSim in a
separate replaceable shared library (`JSBSim.dll` / `libJSBSim.so`) and keeps
the C ABI wrapper separate. Source: <https://github.com/JSBSim-Team/jsbsim>.
The build/cache script obtains and verifies the exact source revision; raw
upstream source and build trees remain outside the repository.

The ABI exposes instance lifetime, aircraft load, initial-condition reset,
controls, steady wind, fixed-step advance, and a blittable state snapshot. No
C++ class crosses into C#.

The KBDU local frame uses FAA ARP coordinates and runway true heading
approximately `89.972°`. Runway designator `08/26` is magnetic; it is not used
as the Unity ENU yaw.

Android compatibility note: JSBSim 1.3.1 assumes `<netdb.h>` transitively
declares `sockaddr_in`, `htons`, and related POSIX socket symbols. Android
bionic does not. The wrapper CMake injects `<netinet/in.h>` and `<arpa/inet.h>`
for JSBSim's `InputOutput` object target; the pinned upstream checkout remains
unmodified. Unity 6000.3.8f1's bundled CMake 3.22/NDK r27 combination also
omits libc++ from this upstream shared-library link, so the CMake target links
the NDK `c++` linker script explicitly while preserving `c++_shared` packaging.
