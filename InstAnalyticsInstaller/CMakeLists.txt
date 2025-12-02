cmake_minimum_required(VERSION 3.20)
project(InstAnalyticsInstaller VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Set Windows subsystem to Windows (GUI application)
if(WIN32)
    set(CMAKE_WIN32_EXECUTABLE ON)
    # Force Unicode character set and UTF-8 source encoding
    add_definitions(-DUNICODE -D_UNICODE)
    add_compile_options(/utf-8)
endif()

# Include directories
include_directories(${CMAKE_SOURCE_DIR}/include)

# Source files
set(SOURCES
    src/main.cpp
    src/DotNetChecker.cpp
    src/Downloader.cpp
    src/Installer.cpp
    src/UIManager.cpp
    src/ZipExtractor.cpp
)

# Header files
set(HEADERS
    include/DotNetChecker.h
    include/Downloader.h
    include/Installer.h
    include/UIManager.h
    include/ZipExtractor.h
    include/Constants.h
)

# Resource file
set(RESOURCES
    resources/installer.rc
)

# Add executable
add_executable(${PROJECT_NAME} WIN32 ${SOURCES} ${HEADERS} ${RESOURCES})

# Link libraries
target_link_libraries(${PROJECT_NAME}
    PRIVATE
    urlmon
    ole32
    shell32
    comctl32
    gdi32
    user32
    wininet
)

# Set output directory
set_target_properties(${PROJECT_NAME} PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

# Set application icon and other properties
if(WIN32)
    set_target_properties(${PROJECT_NAME} PROPERTIES
        WIN32_EXECUTABLE TRUE
        LINK_FLAGS "/MANIFESTUAC:level='requireAdministrator' /MANIFEST:EMBED"
    )
endif()
