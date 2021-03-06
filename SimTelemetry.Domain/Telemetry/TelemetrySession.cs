﻿namespace SimTelemetry.Domain.Telemetry
{
    public class TelemetrySession : ITelemetryObject
    {
        public bool IsActive { get; protected set; }
        public bool IsOffline { get; protected set; }
        public bool IsReplay { get; protected set; }
        public bool IsLoading { get; protected set; }

        public float Time { get; protected set; }
        public int Cars { get; protected set; }

        public void Update(ITelemetry telemetry, IDataProvider Memory)
        {
            IDataNode sessionGroup = Memory.Get("Session");

            Cars = sessionGroup.ReadAs<int>("Cars");
            Time = sessionGroup.ReadAs<float>("Time");

            IsActive = sessionGroup.ReadAs<bool>("IsActive");
            IsOffline = sessionGroup.ReadAs<bool>("IsOffline");
            IsReplay = sessionGroup.ReadAs<bool>("IsReplay");
            IsLoading = sessionGroup.ReadAs<bool>("IsLoading");
        }

        public TelemetrySession Clone()
        {
            return (TelemetrySession)MemberwiseClone();
        }
    }
}