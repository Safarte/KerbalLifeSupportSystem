using System;
using KSP.Sim;
using KSP.Sim.Definitions;
using UnityEngine;
using UnityEngine.Serialization;

namespace KSP2UT.KSP2UnityTools
{
    public class AttachmentNode : MonoBehaviour
    {
        [Tooltip("Optional field that can be used to group nodes together, eg. 2 downward facing nodes grouped into a 'bottom' group. The group ID would be the same on both nodes. Empty means no group, which is default behavior.")]
        public string nodeSymmetryGroupID;
        public AttachNodeType nodeType;
        public AttachNodeMethod attachMethod;
        public bool isMultiJoint;
        public int multiJointMaxJoint;
        public float multiJointRadiusOffset;
        public int size;
        public float visualSize;
        public bool isResourceCrossFeed;
        public bool isRigid;
        public float angularStrengthMultiplier;
        public float contactArea;
        public float overrideDragArea;
        public bool isCompoundJoint;

        
    }
}