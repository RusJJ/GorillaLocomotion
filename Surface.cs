namespace GorillaLocomotion
{
    using UnityEngine;
    public enum SurfaceMaterial : byte
    {
        Default = 0,
        Wood = 1,
        Grass = 2,
        Dirt = 3,

    }
    public class Surface : MonoBehaviour
    {
        public const float flDefaultSlipPercentage = 0.03f;

        public float flSlipPercentage;
        public SurfaceMaterial eSurfaceMaterial;
    }
}