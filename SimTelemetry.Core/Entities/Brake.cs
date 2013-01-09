﻿using SimTelemetry.Core.Enumerations;
using SimTelemetry.Core.ValueObjects;

namespace SimTelemetry.Core.Entities
{
    public class Brake
    {
        public WheelLocation Location { get; private set; }

        public Range OptimumTemperature { get; private set; }

        public Range ThicknessStart { get; private set; }
        public float ThicknessFailure { get; private set; }

        public float Torque { get; private set; }
    }
}