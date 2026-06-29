using UnityEngine;

namespace EasyPeasyFirstPersonController
{
    public class OverrideCenterOfMass : MonoBehaviour
    {
        public Transform centerOfMassTarget;

        private void Awake()
        {
            if (centerOfMassTarget != null)
            {
                GetComponent<Rigidbody>().centerOfMass = transform.InverseTransformPoint(centerOfMassTarget.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (centerOfMassTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(centerOfMassTarget.position, 0.008f);
            }
        }
    }
}