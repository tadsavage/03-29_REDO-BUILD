using System.Linq;
using UnityEngine;

/// <summary>
/// Mirrors the worker Animator's parameters onto the nested modular avatar's own Animator every
/// frame, so the modular body plays the same MaleStaff states the AI drives (the worker Animator is
/// what AgentAnimation writes to). The avatar is parented under the worker, so movement + lifecycle
/// come for free — this only forwards the animation parameters. Generic over bool/int/float params.
/// </summary>
[DisallowMultipleComponent]
public class ModularAvatarRig : MonoBehaviour
{
    private Animator _source;   // worker Animator (AgentAnimation writes its bools)
    private Animator _self;     // this avatar's own Animator
    private AnimatorControllerParameter[] _params;

    private Transform _sampleBone;   // a leg bone, for the one-shot diagnostic
    private float _diagT; private bool _diagDone;

    public void Init(Animator source, Animator self, Transform sampleBone)
    {
        _source = source; _self = self; _sampleBone = sampleBone;
        _params = source != null ? source.parameters : System.Array.Empty<AnimatorControllerParameter>();
    }

    void LateUpdate()
    {
        if (_source == null || _self == null || !_self.isInitialized) return;

        for (int i = 0; i < _params.Length; i++)
        {
            var p = _params[i];
            switch (p.type)
            {
                case AnimatorControllerParameterType.Bool:  _self.SetBool(p.nameHash,    _source.GetBool(p.nameHash));    break;
                case AnimatorControllerParameterType.Float: _self.SetFloat(p.nameHash,   _source.GetFloat(p.nameHash));   break;
                case AnimatorControllerParameterType.Int:   _self.SetInteger(p.nameHash, _source.GetInteger(p.nameHash)); break;
            }
        }

        // One-shot diagnostic ~2s in: confirms the leg is animating AND reports where the mesh is and
        // whether it's actually visible/enabled (so an "invisible" report can be pinned to position vs
        // culling vs disabled). Remove once the walk is confirmed.
        if (!_diagDone)
        {
            _diagT += Time.deltaTime;
            if (_diagT >= 2f)
            {
                _diagDone = true;
                var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
                string rot = _sampleBone != null ? _sampleBone.localRotation.eulerAngles.ToString("F1") : "null";
                string smrInfo = smr != null
                    ? $"smr='{smr.name}' enabled={smr.enabled} active={smr.gameObject.activeInHierarchy} visible={smr.isVisible} " +
                      $"boundsCenter={smr.bounds.center} boundsSize={smr.bounds.size} mesh={(smr.sharedMesh != null)}"
                    : "no SMR found";
                Debug.Log($"[ModularRig] diag '{name}': selfWalk={_self.GetBool("IsWalking")} avatarWorldPos={transform.position} " +
                          $"legEuler={rot} | {smrInfo}");
            }
        }
    }
}
