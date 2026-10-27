# Long ESP-IDF include lists can exceed Windows' process command-line limit.
# Configure Ninja's generator, rather than patching disposable rules.ninja files.
# This moves definitions, include paths and flags together into compiler @files.
if(CMAKE_HOST_WIN32 AND CMAKE_GENERATOR MATCHES "^Ninja")
    set(CMAKE_NINJA_FORCE_RESPONSE_FILE ON)
endif()
