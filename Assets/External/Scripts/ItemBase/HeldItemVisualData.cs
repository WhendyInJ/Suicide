using System;
using UnityEngine;

[Serializable]
public struct HeldItemVisualData
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 localPosition;
    [SerializeField] private Vector3 localEulerAngles;
    [SerializeField] private Vector3 localScale;

    public GameObject Prefab => prefab;
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 LocalScale => localScale == Vector3.zero ? Vector3.one : localScale;

    public bool HasPrefab => prefab != null;
}