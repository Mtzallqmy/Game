using UnityEngine;

namespace Nageel.Vehicle
{
    public class WheelEffects : MonoBehaviour
    {
        public WheelCollider wheelCollider;
        public TrailRenderer skidTrail;
        public ParticleSystem smokeParticles;

        public float slipThreshold = 0.35f;

        private void Update()
        {
            if (wheelCollider == null) return;

            WheelHit hit;
            if (wheelCollider.GetGroundHit(out hit))
            {
                float totalSlip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
                bool isSlipping = totalSlip > slipThreshold;

                if (skidTrail != null)
                {
                    skidTrail.emitting = isSlipping;
                }

                if (smokeParticles != null)
                {
                    if (isSlipping && !smokeParticles.isPlaying)
                    {
                        smokeParticles.Play();
                    }
                    else if (!isSlipping && smokeParticles.isPlaying)
                    {
                        smokeParticles.Stop();
                    }
                }
            }
            else
            {
                if (skidTrail != null) skidTrail.emitting = false;
                if (smokeParticles != null && smokeParticles.isPlaying) smokeParticles.Stop();
            }
        }
    }
}
