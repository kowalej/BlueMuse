#include "pch.h"
#include "LSLLocalClock.h"
#include "chrono"

using namespace NativeHelpers;
using namespace Platform;

double LSLLocalClock::GetNow()
{
	return std::chrono::nanoseconds(std::chrono::high_resolution_clock::now().time_since_epoch()).count() / 1000000000.0;
}
