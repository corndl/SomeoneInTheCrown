﻿using SITC.Entities;
using SITC.Tools;
using UnityEngine;

namespace SITC.AI
{
    #region Data structures
    public enum EAIState
    {
        RoamingPatrol,
        OppressionGoToEntity,
        OppressionTakeAwayEntity,
        OppressedTakenAway,
        Witness
    }
    #endregion Data structures

    [RequireComponent(typeof(Entity))]
    public class EntityAI : SitcBehaviour
    {
        #region Members
        [SerializeField]
        private float _witnessDuration = 2f;
        #endregion Members

        #region Private members
        private Entity _entity = null;
        private EAIState _currentState = EAIState.RoamingPatrol;

        // Roaming
        private Transform _target = null;
        private float _targetReachedTime = 0f;
        private float _delayBeforeNextTarget = 0f;

        // Go to entity
        private Entity _targetEntity = null;

        private float _witnessTime = 0f;
        private float _currentSpeed = 1f;
        private bool _oppressor = false;
        #endregion Private members

        #region Getters
        private Entity Entity { get { _entity = _entity ?? GetComponent<Entity>(); return _entity; } }
        #endregion Getters

        #region Lifecycle
        protected override void DoUpdate()
        {
            base.DoUpdate();

            if (! _oppressor
                && Entity.GetConviction() == -1f)
            {
                _oppressor = true;
                _currentState = EAIState.OppressionGoToEntity;
            }

            switch (_currentState)
            {
                case EAIState.RoamingPatrol:
                case EAIState.OppressedTakenAway:
                case EAIState.OppressionTakeAwayEntity:
                    Pathfinding();
                    break;

                case EAIState.Witness:
                    if (! CheckWitness())
                    {
                        _currentState = EAIState.RoamingPatrol;
                    }
                    Pathfinding();
                    break;

                case EAIState.OppressionGoToEntity:
                    if (_targetEntity == null)
                    {
                        _targetEntity = EntityManager.GetOppressionTarget(Entity);
                    }
                    if (_targetEntity == null)
                    {
                        _currentState = EAIState.RoamingPatrol;
                    }

                    Pathfinding();
                    break;
            }
        }
        #endregion Lifecycle

        #region API
        public void Alert(float conviction)
        {
            if (_currentState != EAIState.Witness)
            {
                return;
            }

            Debug.Log(name + " was alerted");
            Entity.AddConviction(conviction);
        }

        public void SetWitness()
        {
            _witnessTime = Time.time;
        }

        public void SetExitTarget(Transform exit, float delay)
        {
            _currentSpeed = AiConfiguration.TakeAwaySpeedRatio;
            _currentState = EAIState.OppressedTakenAway;
            _target = exit;
            _delayBeforeNextTarget = delay;
            _targetReachedTime = Time.time;
        }
        
        public EAIState GetState()
        {
            return _currentState;
        }
        #endregion API

        #region Pathfinding
        private void Pathfinding()
        {
            if (_targetReachedTime + _delayBeforeNextTarget > Time.time)
            {
                return;
            }

            if (ReachedCurrentTarget())
            {
                _targetReachedTime = Time.time;
                _currentSpeed = Random.Range(AiConfiguration.MinimumSpeedRatio, 1f);
                _delayBeforeNextTarget = Random.Range(0f, AiConfiguration.MaxDelayBeforeNextTarget);

                switch (_currentState)
                {
                    case EAIState.OppressedTakenAway:
                        _currentSpeed = Random.Range(AiConfiguration.MinimumSpeedRatio, 1f);
                        _currentState = EAIState.RoamingPatrol;
                        _delayBeforeNextTarget = Random.Range(AiConfiguration.MinMaxTakenAwayCooldownBeforeReturn.x, AiConfiguration.MinMaxTakenAwayCooldownBeforeReturn.y);
                        break;

                    case EAIState.RoamingPatrol:
                        _target = AiPatrolPoints.GetNextTarget(transform.position, Entity.GetConviction());
                        break;

                    case EAIState.OppressionGoToEntity:
                        _currentState = EAIState.OppressionTakeAwayEntity;
                        EntityManager.TakeAway(Entity, _targetEntity);
                        _target = AiExitPoints.GetClosestExit(transform.position);
                        _currentSpeed = AiConfiguration.TakeAwaySpeedRatio;

                        if (_targetEntity.GetComponent<EntityAI>())
                        {
                            _targetEntity.GetComponent<EntityAI>().SetExitTarget(_target, _delayBeforeNextTarget);
                        }
                        break;

                    case EAIState.OppressionTakeAwayEntity:
                        _currentState = EAIState.RoamingPatrol;
                        _target = AiPatrolPoints.GetNextTargetAfterTakeAway(transform.position);
                        break;
                }
            }

            MoveTowardsTarget();
        }

        private bool ReachedCurrentTarget()
        {
            Transform target = GetTarget();

            if (target == null)
            {
                return true;
            }

            return Vector3.Distance(transform.position, target.position) <= AiConfiguration.TargetReachedDistance;
        }

        private void MoveTowardsTarget()
        {
            Transform target = GetTarget();
            if (target == null)
            {
                return;
            }

            Vector3 direction = (target.position - transform.position).normalized * _currentSpeed;
            Entity.Move(direction);
        }

        private Transform GetTarget()
        {
            switch (_currentState)
            {
                case EAIState.OppressionGoToEntity:
                    return _targetEntity.transform;

                case EAIState.RoamingPatrol:
                case EAIState.OppressionTakeAwayEntity:
                case EAIState.OppressedTakenAway:
                    return _target;
            }

            return null;
        }
        #endregion Pathfinding

        #region Alert
        private bool CheckWitness()
        {
            return _witnessTime + _witnessDuration > Time.time;
        }
        #endregion Alert

        #region Oppression
        private void AcquireTarget()
        {

        }
        #endregion Oppression
    }
}