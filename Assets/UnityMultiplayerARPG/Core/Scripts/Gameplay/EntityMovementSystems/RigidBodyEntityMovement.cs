﻿using LiteNetLib.Utils;
using LiteNetLibManager;
using StandardAssets.Characters.Physics;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplayerARPG
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(OpenCharacterController))]
    public class RigidBodyEntityMovement : BaseNetworkedGameEntityComponent<BaseGameEntity>, IEntityMovementComponent, IBuiltInEntityMovement3D
    {
        [Header("Movement AI")]
        [Range(0.01f, 1f)]
        public float stoppingDistance = 0.1f;
        public MovementSecure movementSecure = MovementSecure.NotSecure;

        [Header("Movement Settings")]
        public float jumpHeight = 2f;
        public ApplyJumpForceMode applyJumpForceMode = ApplyJumpForceMode.ApplyImmediately;
        public float applyJumpForceFixedDuration;
        public float backwardMoveSpeedRate = 0.75f;
        public float gravity = 9.81f;
        public float maxFallVelocity = 40f;
        public LayerMask platformLayerMask = 1;
        [Tooltip("Delay before character change from grounded state to airborne")]
        public float airborneDelay = 0.01f;
        public bool doNotChangeVelocityWhileAirborne;
        public float landedPauseMovementDuration = 0f;
        public float beforeCrawlingPauseMovementDuration = 0f;
        public float afterCrawlingPauseMovementDuration = 0f;
        [Range(0.1f, 1f)]
        public float underWaterThreshold = 0.75f;
        public bool autoSwimToSurface;

        [Header("Root Motion Settings")]
        public bool useRootMotionForMovement;
        public bool useRootMotionForAirMovement;
        public bool useRootMotionForJump;
        public bool useRootMotionForFall;
        public bool useRootMotionWhileNotMoving;
        public bool useRootMotionUnderWater;

        [Header("Networking Settings")]
        public float snapThreshold = 5.0f;

        public Animator CacheAnimator { get; private set; }
        public Rigidbody CacheRigidbody { get; private set; }
        public CapsuleCollider CacheCapsuleCollider { get; private set; }
        public OpenCharacterController CacheOpenCharacterController { get; private set; }
        public BuiltInEntityMovementFunctions3D Functions { get; private set; }

        public float StoppingDistance { get { return Functions.StoppingDistance; } }
        public MovementState MovementState { get { return Functions.MovementState; } }
        public ExtraMovementState ExtraMovementState { get { return Functions.ExtraMovementState; } }
        public DirectionVector2 Direction2D { get { return Functions.Direction2D; } set { Functions.Direction2D = value; } }
        public float CurrentMoveSpeed { get { return Functions.CurrentMoveSpeed; } }
        public Queue<Vector3> NavPaths { get { return Functions.NavPaths; } }
        public bool HasNavPaths { get { return Functions.HasNavPaths; } }

        public override void EntityAwake()
        {
            // Prepare animator component
            CacheAnimator = GetComponent<Animator>();
            // Prepare rigidbody component
            CacheRigidbody = gameObject.GetOrAddComponent<Rigidbody>();
            // Prepare collider component
            CacheCapsuleCollider = gameObject.GetOrAddComponent<CapsuleCollider>();
            // Prepare open character controller
            float radius = CacheCapsuleCollider.radius;
            float height = CacheCapsuleCollider.height;
            Vector3 center = CacheCapsuleCollider.center;
            CacheOpenCharacterController = gameObject.GetOrAddComponent<OpenCharacterController>((comp) =>
            {
                comp.SetRadiusHeightAndCenter(radius, height, center, true, true);
            });
            CacheOpenCharacterController.collision += OnCharacterControllerCollision;
            // Disable unused component
            LiteNetLibTransform disablingComp = gameObject.GetComponent<LiteNetLibTransform>();
            if (disablingComp != null)
            {
                Logging.LogWarning(nameof(RigidBodyEntityMovement), "You can remove `LiteNetLibTransform` component from game entity, it's not being used anymore [" + name + "]");
                disablingComp.enabled = false;
            }
            // Setup
            Functions = new BuiltInEntityMovementFunctions3D(Entity, CacheAnimator, this)
            {
                stoppingDistance = stoppingDistance,
                movementSecure = movementSecure,
                jumpHeight = jumpHeight,
                applyJumpForceMode = applyJumpForceMode,
                applyJumpForceFixedDuration = applyJumpForceFixedDuration,
                backwardMoveSpeedRate = backwardMoveSpeedRate,
                gravity = gravity,
                maxFallVelocity = maxFallVelocity,
                stickGroundForce = 0f,
                airborneDelay = airborneDelay,
                doNotChangeVelocityWhileAirborne = doNotChangeVelocityWhileAirborne,
                landedPauseMovementDuration = landedPauseMovementDuration,
                beforeCrawlingPauseMovementDuration = beforeCrawlingPauseMovementDuration,
                afterCrawlingPauseMovementDuration = afterCrawlingPauseMovementDuration,
                underWaterThreshold = underWaterThreshold,
                autoSwimToSurface = autoSwimToSurface,
                useRootMotionForMovement = useRootMotionForMovement,
                useRootMotionForAirMovement = useRootMotionForAirMovement,
                useRootMotionForJump = useRootMotionForJump,
                useRootMotionForFall = useRootMotionForFall,
                useRootMotionWhileNotMoving = useRootMotionWhileNotMoving,
                useRootMotionUnderWater = useRootMotionUnderWater,
                snapThreshold = snapThreshold,
            };
            Functions.StopMoveFunction();
        }

        public override void EntityStart()
        {
            Functions.EntityStart();
            CacheOpenCharacterController.SetPosition(CacheTransform.position, true);
        }

        public override void ComponentOnEnable()
        {
            Functions.ComponentEnabled();
            CacheOpenCharacterController.enabled = true;
            CacheOpenCharacterController.SetPosition(CacheTransform.position, true);
        }

        public override void ComponentOnDisable()
        {
            CacheOpenCharacterController.enabled = false;
        }

        public override void EntityOnDestroy()
        {
            base.EntityOnDestroy();
            CacheOpenCharacterController.collision -= OnCharacterControllerCollision;
        }

        public override void OnSetOwnerClient(bool isOwnerClient)
        {
            base.OnSetOwnerClient(isOwnerClient);
            Functions.OnSetOwnerClient(isOwnerClient);
        }

        private void OnAnimatorMove()
        {
            Functions.OnAnimatorMove();
        }

        private void OnTriggerEnter(Collider other)
        {
            Functions.OnTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            Functions.OnTriggerExit(other);
        }

        private void OnCharacterControllerCollision(OpenCharacterController.CollisionInfo hit)
        {
            Functions.OnControllerColliderHit(hit.point, hit.transform);
        }

        public override void EntityUpdate()
        {
            float deltaTime = Time.deltaTime;
            Functions.UpdateMovement(deltaTime);
            Functions.UpdateRotation(deltaTime);
            Functions.AfterMovementUpdate(deltaTime);
        }

        public bool GroundCheck()
        {
            return CacheOpenCharacterController.isGrounded;
        }

        public Bounds GetBounds()
        {
            return CacheCapsuleCollider.bounds;
        }

        public void Move(Vector3 motion)
        {
            CacheOpenCharacterController.Move(motion);
        }

        public void RotateY(float yAngle)
        {
            CacheTransform.eulerAngles = new Vector3(0f, yAngle, 0f);
        }

        public void OnJumpForceApplied()
        {

        }

        public bool WriteClientState(NetDataWriter writer, out bool shouldSendReliably)
        {
            return Functions.WriteClientState(writer, out shouldSendReliably);
        }

        public bool WriteServerState(NetDataWriter writer, out bool shouldSendReliably)
        {
            return Functions.WriteServerState(writer, out shouldSendReliably);
        }

        public void ReadClientStateAtServer(NetDataReader reader)
        {
            Functions.ReadClientStateAtServer(reader);
        }

        public void ReadServerStateAtClient(NetDataReader reader)
        {
            Functions.ReadServerStateAtClient(reader);
        }

        public void StopMove()
        {
            Functions.StopMove();
        }

        public void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            Functions.KeyMovement(moveDirection, movementState);
        }

        public void PointClickMovement(Vector3 position)
        {
            Functions.PointClickMovement(position);
        }

        public void SetExtraMovementState(ExtraMovementState extraMovementState)
        {
            Functions.SetExtraMovementState(extraMovementState);
        }

        public void SetLookRotation(Quaternion rotation)
        {
            Functions.SetLookRotation(rotation);
        }

        public Quaternion GetLookRotation()
        {
            return Functions.GetLookRotation();
        }

        public void SetSmoothTurnSpeed(float speed)
        {
            Functions.SetSmoothTurnSpeed(speed);
        }

        public float GetSmoothTurnSpeed()
        {
            return Functions.GetSmoothTurnSpeed();
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            Functions.Teleport(position, rotation);
        }

        public bool FindGroundedPosition(Vector3 fromPosition, float findDistance, out Vector3 result)
        {
            return Functions.FindGroundedPosition(fromPosition, findDistance, out result);
        }

#if UNITY_EDITOR
        [ContextMenu("Applies Collider Settings To Controller")]
        public void AppliesColliderSettingsToController()
        {
            CapsuleCollider collider = gameObject.GetOrAddComponent<CapsuleCollider>();
            float radius = collider.radius;
            float height = collider.height;
            Vector3 center = collider.center;
            // Prepare open character controller
            OpenCharacterController controller = gameObject.GetOrAddComponent<OpenCharacterController>();
            controller.SetRadiusHeightAndCenter(radius, height, center, true, true);
        }
#endif
    }
}
