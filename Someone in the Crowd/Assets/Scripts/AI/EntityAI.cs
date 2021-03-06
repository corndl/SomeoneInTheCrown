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
        private SpriteRenderer _witnessSign = null;
        [SerializeField]
        private SpriteRenderer _violenceSign = null;
        #endregion Members

        #region Private members
        private Entity _entity = null;
        private EAIState _currentState = EAIState.RoamingPatrol;
        private EAIState _previousState = EAIState.RoamingPatrol;

        // Roaming
        private Transform _target = null;
        private float _targetReachedTime = 0f;
        private float _delayBeforeNextTarget = 0f;
        private float _currentSpeed = 1f;

        // Oppression
        private bool _oppressor = false;
        private Entity _targetEntity = null;
        private float _tookAwayTime = 0f;
        private float _delayBeforeTakeAway = 0f;

        private float _witnessTime = 0f;
        private float _witnessDuration = 2f;

        private float _pursuitDuration = 0f;
        #endregion Private members

        #region Getters
        private Entity Entity { get { _entity = _entity ?? GetComponent<Entity>(); return _entity; } }
        #endregion Getters

        #region Lifecycle
        protected override void Init()
        {
            base.Init();
            if (_witnessSign != null)
            {
                _witnessSign.gameObject.SetActive(false);
            }
            if (_violenceSign != null)
            {
                _violenceSign.gameObject.SetActive(false);
            }
        }

        protected override void DoUpdate()
        {
            base.DoUpdate();

            if (! _oppressor
                && Entity.GetConviction() == -1f)
            {
                _oppressor = true;
                _currentState = EAIState.OppressionGoToEntity;
                _pursuitDuration = 0f;
            }
            else if (_oppressor
                && Entity.GetConviction() != -1f)
            {
                _oppressor = false;
                _currentState = EAIState.RoamingPatrol;
            }

            if (_tookAwayTime != 0f
                && Time.time > _tookAwayTime + _delayBeforeTakeAway)
            {
                _tookAwayTime = 0f;
                _currentState = EAIState.OppressionGoToEntity;
                _pursuitDuration = 0f;
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
                        if (_witnessSign != null)
                        {
                            _witnessSign.gameObject.SetActive(false);
                        }
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
                    if (_targetEntity != null)
                    {
                        _pursuitDuration += Time.deltaTime;
                        if (_pursuitDuration >= AiConfiguration.MaxPursuitDuration)
                        {
                            _currentState = EAIState.RoamingPatrol;
                            _targetEntity = null;
                        }
                    }

                    Pathfinding();
                    break;
            }
        }
        #endregion Lifecycle

        #region API
        public bool Alert(float conviction)
        {
            if (_currentState != EAIState.Witness)
            {
                return false;
            }

            Audio.AudioManager.Alerted();
            Debug.Log(name + " was alerted");
            Entity.AddConviction(conviction);
            return true;
        }

        public void SetWitness(float duration)
        {
            if (Entity.GetConviction() == -1)
            {
                return;
            }

            if (_witnessSign != null)
            {
                _witnessSign.gameObject.SetActive(true);
            }
            _currentState = EAIState.Witness;
            _witnessTime = Time.time;
            _witnessDuration = duration;
        }

        public void SetExitTarget(Transform exit, float delay)
        {
            _currentSpeed = AiConfiguration.TakeAwaySpeedRatio;
            _currentState = EAIState.OppressedTakenAway;
            _target = exit;
            _delayBeforeNextTarget = delay;
            _targetReachedTime = Time.time;

            Audio.AudioManager.TakeAway();
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

                if (_previousState == EAIState.OppressedTakenAway)
                {
                    EntityManager.RemoveTaken(Entity);
                }
                if (_violenceSign != null)
                {
                    _violenceSign.gameObject.SetActive(false);
                }

                _previousState = _currentState;

                switch (_currentState)
                {
                    case EAIState.OppressedTakenAway:
                        _currentSpeed = Random.Range(AiConfiguration.MinimumSpeedRatio, 1f);
                        _currentState = EAIState.RoamingPatrol;
                        _delayBeforeNextTarget = Random.Range(AiConfiguration.MinMaxTakenAwayCooldownBeforeReturn.x, AiConfiguration.MinMaxTakenAwayCooldownBeforeReturn.y);
                        Entity.SetConviction(0f, true);
                        break;

                    case EAIState.RoamingPatrol:
                        _target = AiPatrolPoints.GetNextTarget(transform.position, Entity.GetConviction());
                        break;

                    case EAIState.OppressionGoToEntity:
                        _currentState = EAIState.OppressionTakeAwayEntity;
                        EntityManager.TakeAway(Entity, _targetEntity);

                        if (_violenceSign != null)
                        {
                            _violenceSign.gameObject.SetActive(true);
                        }

                        _target = AiExitPoints.GetClosestExit(transform.position);
                        _currentSpeed = AiConfiguration.TakeAwaySpeedRatio;

                        if (_targetEntity.GetComponent<EntityAI>())
                        {
                            _targetEntity.GetComponent<EntityAI>().SetExitTarget(_target, _delayBeforeNextTarget);
                        }
                        break;

                    case EAIState.OppressionTakeAwayEntity:
                        Entity.SetConviction(AiConfiguration.ConvictionAfterTakeAway, true);
                        _currentState = EAIState.RoamingPatrol;
                        _target = AiPatrolPoints.GetNextTargetAfterTakeAway(transform.position);
                        _targetEntity = null;
                        _tookAwayTime = Time.time;
                        _delayBeforeTakeAway = Random.Range(AiConfiguration.MinMaxTookAwayCooldownBeforeOppression.x, AiConfiguration.MinMaxTookAwayCooldownBeforeOppression.y);
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