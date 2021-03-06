﻿using SITC.AI;
using SITC.Tools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SITC.Controls
{
    [RequireComponent(typeof(Entity), typeof(InputHandler))]
    public class AlertCone : SitcBehaviour
    {
        #region Members
        [Header("Size"), SerializeField]
        private float _coneMinimumSize = 1f;
        [SerializeField]
        private float _coneMaximumSize = 5f;
        [SerializeField]
        private float _coneGrowthFactor = 1f;

        [Header("Angle"), SerializeField]
        private float _coneMinimumAngle = 45f;
        [SerializeField]
        private float _coneMaximumAngle = 45f;
        [SerializeField]
        private float _coneAngleGrowthFactor = 10f;

        [Header("Rendering"), SerializeField]
        private LineRenderer _leftRenderer = null;
        [SerializeField]
        private LineRenderer _rightRenderer = null;
        [SerializeField]
        private LineRenderer _jointRenderer = null;

        [Header("Cooldown duration"), SerializeField]
        private float _cooldowDuration = 0f;

        [SerializeField]
        private GameObject _sign = null;
        [SerializeField]
        private float _signDuration = .5f;
        #endregion Members

        #region Private members
        private float _cone = 0f;
        private float _coneAngle = 0f;
        private Vector3 _left = Vector3.zero;
        private Vector3 _right = Vector3.zero;
        private Entity _entity = null;
        private float _alertTime = 0f;
        #endregion Private members

        #region Getters
        private Entity Entity { get { _entity = _entity ?? GetComponent<Entity>(); return _entity; } }
        #endregion Getters

        #region Lifecycle
        protected override void Init()
        {
            base.Init();

            if (_sign)
            {
                _sign.SetActive(false);
            }
        }

        protected override void DoUpdate()
        {
            base.DoUpdate();

            if (_signDuration + _alertTime < Time.time
                || _alertTime == 0f)
            {
                if (_sign)
                {
                    _sign.SetActive(false);
                }
            }
            else
            {
                if (_sign)
                {
                    _sign.SetActive(true);
                }
            }
        }
        #endregion Lifecycle

        #region API
        public bool IsActive()
        {
            return _cone > 0f && _coneAngle > 0f;
        }

        public bool InCooldown()
        {
            if (_alertTime == 0f)
            {
                return false;
            }

            return _alertTime + _cooldowDuration > Time.time;
        }

        public void GrowCone()
        {
            if (_cone == 0f)
            {
                _cone = _coneMinimumSize;
                _alertTime = 0f;
            }
            else if (_cone < _coneMaximumSize)
            {
                _cone += _coneGrowthFactor * Time.deltaTime;
            }

            if (_coneAngle == 0f)
            {
                _coneAngle = _coneMinimumAngle;
            }
            else if (_coneAngle < _coneMaximumAngle)
            {
                _coneAngle += _coneAngleGrowthFactor * Time.deltaTime;
            }
        }

        public void DrawCone(Vector3 mousePosition)
        {
            Vector3 direction = mousePosition - transform.position;
            direction = direction.normalized;

            _left = direction.RotateInPlane(-_coneAngle / 2) * _cone + transform.position;
            _right = direction.RotateInPlane(_coneAngle / 2) * _cone + transform.position;

            if (_leftRenderer != null)
            {
                _leftRenderer.positionCount = 2;
                _leftRenderer.SetPositions(new Vector3[] { transform.position, _left });
            }

            if (_rightRenderer != null)
            {
                _rightRenderer.positionCount = 2;
                _rightRenderer.SetPositions(new Vector3[] { transform.position, _right });
            }

            if (_jointRenderer != null)
            {
                _jointRenderer.positionCount = 2;
                _jointRenderer.SetPositions(new Vector3[] { _left, _right });
            }
        }

        public void StopCone(bool cancel)
        {
            if (!cancel)
            {
                AlertBypassers();

                if (_cone > 0f)
                {
                    _alertTime = Time.time;
                    Audio.AudioManager.Alert();

                    if (_sign)
                    {
                        _sign.SetActive(true);
                    }
                }
            }

            _cone = 0f;
            _coneAngle = 0f;
            _left = Vector3.zero;
            _right = Vector3.zero;

            if (_rightRenderer != null)
            {
                _rightRenderer.positionCount = 0;
            }
            if (_leftRenderer != null)
            {
                _leftRenderer.positionCount = 0;
            }
            if (_jointRenderer != null)
            {
                _jointRenderer.positionCount = 0;
            }
        }
        #endregion API

        #region Alert
        private void AlertBypassers()
        {
            List<EntityAI> entities = FindObjectsOfType<EntityAI>().ToList();
            entities.RemoveAll(ai => !InCone(ai.transform.position, _left, _right));

            if (entities.Count == 0)
            {
                return;
            }

            float conviction = Entity.GetConviction();
            float intensity = EntityConfiguration.AlertConvictionImpact.Evaluate(conviction);
            intensity /= entities.Count;
            float convToAddToSelf = 0f;

            foreach (var ai in entities)
            {
                if (ai.Alert(intensity))
                {
                    convToAddToSelf += intensity;
                }
            }

            if (convToAddToSelf > 0f)
            {
                Debug.Log("Added " + convToAddToSelf + " to self conviction after alert");
                Entity.AddConviction(convToAddToSelf);
            }
        }

        private bool InCone(Vector3 point, Vector3 left, Vector3 right)
        {
            Vector3 v0 = right - transform.position;
            Vector3 v1 = left - transform.position;
            Vector3 v2 = point - transform.position;

            // Compute dot products
            float dot00 = Vector3.Dot(v0, v0);
            float dot01 = Vector3.Dot(v0, v1);
            float dot02 = Vector3.Dot(v0, v2);
            float dot11 = Vector3.Dot(v1, v1);
            float dot12 = Vector3.Dot(v1, v2);

            // Compute barycentric coordinates
            float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // Check if point is in triangle
            return (u >= 0) && (v >= 0) && (u + v < 1);
        }
        #endregion Alert
    }
}