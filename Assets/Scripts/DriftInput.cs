namespace DriftX
{
    /// <summary>Shared input state written by TouchControls, read by DriftCar.</summary>
    public static class DriftInput
    {
        public static float Steer;     // -1 left .. +1 right
        public static float Throttle;  // 0..1
        public static float Brake;     // 0..1 (also reverse when stopped)
        public static bool Handbrake;
    }
}
