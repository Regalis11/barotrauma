﻿using FarseerPhysics;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using System;

namespace Barotrauma
{
    class FishAnimController : AnimController
    {
        public override RagdollParams RagdollParams
        {
            get { return FishRagdollParams; }
            protected set { FishRagdollParams = value as FishRagdollParams; }
        }

        private FishRagdollParams _ragdollParams;
        public FishRagdollParams FishRagdollParams
        {
            get
            {
                if (_ragdollParams == null)
                {
                    _ragdollParams = FishRagdollParams.GetDefaultRagdollParams(character.SpeciesName);
                }
                return _ragdollParams;
            }
            protected set
            {
                _ragdollParams = value;
            }
        }

        private FishWalkParams _fishWalkParams;
        public FishWalkParams FishWalkParams
        {
            get
            {
                if (_fishWalkParams == null)
                {
                    _fishWalkParams = FishWalkParams.GetDefaultAnimParams(character);
                }
                return _fishWalkParams;
            }
            set { _fishWalkParams = value; }
        }

        private FishRunParams _fishRunParams;
        public FishRunParams FishRunParams
        {
            get
            {
                if (_fishRunParams == null)
                {
                    _fishRunParams = FishRunParams.GetDefaultAnimParams(character);
                }
                return _fishRunParams;
            }
            set { _fishRunParams = value; }
        }

        private FishSwimSlowParams _fishSwimSlowParams;
        public FishSwimSlowParams FishSwimSlowParams
        {
            get
            {
                if (_fishSwimSlowParams == null)
                {
                    _fishSwimSlowParams = FishSwimSlowParams.GetDefaultAnimParams(character);
                }
                return _fishSwimSlowParams;
            }
            set { _fishSwimSlowParams = value; }
        }

        private FishSwimFastParams _fishSwimFastParams;
        public FishSwimFastParams FishSwimFastParams
        {
            get
            {
                if (_fishSwimFastParams == null)
                {
                    _fishSwimFastParams = FishSwimFastParams.GetDefaultAnimParams(character);
                }
                return _fishSwimFastParams;
            }
            set { _fishSwimFastParams = value; }
        }

        public IFishAnimation CurrentFishAnimation => CurrentAnimationParams as IFishAnimation;
        public new FishGroundedParams CurrentGroundedParams => base.CurrentGroundedParams as FishGroundedParams;
        public new FishSwimParams CurrentSwimParams => base.CurrentSwimParams as FishSwimParams;

        public float? TailAngle => GetValidOrNull(CurrentAnimationParams, CurrentFishAnimation?.TailAngleInRadians);
        public float FootTorque => CurrentFishAnimation.FootTorque;
        public float HeadTorque => CurrentFishAnimation.HeadTorque;
        public float TorsoTorque => CurrentFishAnimation.TorsoTorque;
        public float TailTorque => CurrentFishAnimation.TailTorque;
        public float HeadMoveForce => CurrentGroundedParams.HeadMoveForce;
        public float TorsoMoveForce => CurrentGroundedParams.TorsoMoveForce;
        public float FootMoveForce => CurrentGroundedParams.FootMoveForce;

        public override GroundedMovementParams WalkParams
        {
            get { return FishWalkParams; }
            set { FishWalkParams = value as FishWalkParams; }
        }

        public override GroundedMovementParams RunParams
        {
            get { return FishRunParams; }
            set { FishRunParams = value as FishRunParams; }
        }

        public override SwimParams SwimSlowParams
        {
            get { return FishSwimSlowParams; }
            set { FishSwimSlowParams = value as FishSwimSlowParams; }
        }

        public override SwimParams SwimFastParams
        {
            get { return FishSwimFastParams; }
            set { FishSwimFastParams = value as FishSwimFastParams; }
        }

        private float flipTimer;

        public FishAnimController(Character character, string seed, FishRagdollParams ragdollParams = null) : base(character, seed, ragdollParams) { }

        public override void UpdateAnim(float deltaTime)
        {
            if (Frozen) return;

            if (character.IsDead || character.IsUnconscious || character.Stun > 0.0f)
            {
                Collider.Enabled = false;
                Collider.FarseerBody.FixedRotation = false;
                //set linear velocity even though the collider is disabled, 
                //because the character won't be able to switch back from ragdoll mode until the velocity of the collider is low enough
                Collider.LinearVelocity = MainLimb.LinearVelocity;
                Collider.SetTransformIgnoreContacts(MainLimb.SimPosition, MainLimb.Rotation);

                if (character.IsDead && deathAnimTimer < deathAnimDuration)
                {
                    deathAnimTimer += deltaTime;
                    UpdateDying(deltaTime);                    
                }
                
                return;
            }
            else
            {
                deathAnimTimer = 0.0f;
            }

            //re-enable collider
            if (!Collider.Enabled)
            {
                var lowestLimb = FindLowestLimb();

                Collider.SetTransform(new Vector2(
                    Collider.SimPosition.X,
                    Math.Max(lowestLimb.SimPosition.Y + (Collider.radius + Collider.height / 2), Collider.SimPosition.Y)),
                    0.0f);

                Collider.Enabled = true;
            }

            ResetPullJoints();

            if (strongestImpact > 0.0f)
            {
                character.Stun = MathHelper.Clamp(strongestImpact * 0.5f, character.Stun, 5.0f);
                strongestImpact = 0.0f;
            }

            if (inWater && !forceStanding)
            {
                Collider.FarseerBody.FixedRotation = false;
                UpdateSineAnim(deltaTime);
            }
            else if (CanEnterSubmarine && (currentHull != null || forceStanding) && CurrentGroundedParams != null)
            {
                //rotate collider back upright
                float standAngle = dir == Direction.Right ? CurrentGroundedParams.ColliderStandAngleInRadians : -CurrentGroundedParams.ColliderStandAngleInRadians;
                if (Math.Abs(MathUtils.GetShortestAngle(Collider.Rotation, standAngle)) > 0.001f)
                {
                    Collider.AngularVelocity = MathUtils.GetShortestAngle(Collider.Rotation, standAngle) * 60.0f;
                    Collider.FarseerBody.FixedRotation = false;
                }
                else
                {
                    Collider.FarseerBody.FixedRotation = true;
                }

                UpdateWalkAnim(deltaTime);
            }

            //don't flip or drag when simply physics is enabled
            if (SimplePhysicsEnabled) { return; }
            
            if (!character.IsRemotePlayer && (character.AIController == null || character.AIController.CanFlip))
            {
                if (!inWater || (CurrentSwimParams != null && CurrentSwimParams.Mirror))
                {
                    if (targetMovement.X > 0.1f && targetMovement.X > Math.Abs(targetMovement.Y) * 0.5f)
                    {
                        TargetDir = Direction.Right;
                    }
                    else if (targetMovement.X < -0.1f && targetMovement.X < -Math.Abs(targetMovement.Y) * 0.5f)
                    {
                        TargetDir = Direction.Left;
                    }
                }
                else
                {
                    float refAngle = 0.0f;
                    Limb refLimb = GetLimb(LimbType.Head);
                    if (refLimb == null)
                    {
                        refAngle = CurrentAnimationParams.TorsoAngleInRadians;
                        refLimb = GetLimb(LimbType.Torso);
                    }
                    else
                    {
                        refAngle = CurrentAnimationParams.HeadAngleInRadians;
                    }

                    float rotation = refLimb.Rotation;
                    if (!float.IsNaN(refAngle)) { rotation -= refAngle * Dir; }

                    rotation = MathHelper.ToDegrees(MathUtils.WrapAngleTwoPi(rotation));

                    if (rotation < 0.0f) rotation += 360;

                    if (rotation > 20 && rotation < 160)
                    {
                        TargetDir = Direction.Left;
                    }
                    else if (rotation > 200 && rotation < 340)
                    {
                        TargetDir = Direction.Right;
                    }
                }
            }

            if (character.SelectedCharacter != null) DragCharacter(character.SelectedCharacter, deltaTime);

            if (!CurrentFishAnimation.Flip) return;
            if (character.AIController != null && !character.AIController.CanFlip) return;

            flipTimer += deltaTime;

            if (TargetDir != Direction.None && TargetDir != dir) 
            {
                if (flipTimer > 1.0f || character.IsRemotePlayer)
                {
                    Flip();
                    if (!inWater || (CurrentSwimParams != null && CurrentSwimParams.Mirror))
                    {
                        Mirror();
                    }
                    flipTimer = 0.0f;
                }
            }
        }

        private float eatTimer = 0.0f;

        public override void DragCharacter(Character target, float deltaTime)
        {
            if (target == null) return;
            
            Limb mouthLimb = Array.Find(Limbs, l => l != null && l.MouthPos.HasValue);
            if (mouthLimb == null) mouthLimb = GetLimb(LimbType.Head);

            if (mouthLimb == null)
            {
                DebugConsole.ThrowError("Character \"" + character.SpeciesName + "\" failed to eat a target (a head or a limb with a mouthpos required)");
                return;
            }

            Character targetCharacter = target;
            float eatSpeed = character.Mass / targetCharacter.Mass * 0.1f;
            eatTimer += deltaTime * eatSpeed;

            Vector2 mouthPos = GetMouthPosition().Value;
            Vector2 attackSimPosition = character.Submarine == null ? ConvertUnits.ToSimUnits(target.WorldPosition) : target.SimPosition;

            Vector2 limbDiff = attackSimPosition - mouthPos;
            float limbDist = limbDiff.Length();
            if (limbDist < 1.0f)
            {
                //pull the target character to the position of the mouth
                //(+ make the force fluctuate to waggle the character a bit)
                targetCharacter.AnimController.MainLimb.MoveToPos(mouthPos, (float)(Math.Sin(eatTimer) + 10.0f));
                targetCharacter.AnimController.MainLimb.body.SmoothRotate(mouthLimb.Rotation);
                targetCharacter.AnimController.Collider.MoveToPos(mouthPos, (float)(Math.Sin(eatTimer) + 10.0f));

                //pull the character's mouth to the target character (again with a fluctuating force)
                float pullStrength = (float)(Math.Sin(eatTimer) * Math.Max(Math.Sin(eatTimer * 0.5f), 0.0f));
                mouthLimb.body.ApplyForce(limbDiff * mouthLimb.Mass * 50.0f * pullStrength);

                character.ApplyStatusEffects(ActionType.OnEating, deltaTime);

                if (eatTimer % 1.0f < 0.5f && (eatTimer - deltaTime * eatSpeed) % 1.0f > 0.5f)
                {
                    //apply damage to the target character to get some blood particles flying 
                    targetCharacter.AnimController.MainLimb.AddDamage(targetCharacter.SimPosition, 0.0f, 20.0f, 0.0f, false);

                    //keep severing joints until there is only one limb left
                    LimbJoint[] nonSeveredJoints = Array.FindAll(targetCharacter.AnimController.LimbJoints,
                        l => !l.IsSevered && l.CanBeSevered && l.LimbA != null && !l.LimbA.IsSevered && l.LimbB != null && !l.LimbB.IsSevered);
                    if (nonSeveredJoints.Length == 0)
                    {
                        //only one limb left, the character is now full eaten
                        Entity.Spawner.AddToRemoveQueue(targetCharacter);
                        character.SelectedCharacter = null;
                    }
                    else //sever a random joint
                    {
                        targetCharacter.AnimController.SeverLimbJoint(nonSeveredJoints[Rand.Int(nonSeveredJoints.Length)]);
                    }
                }
            }
            else
            {
                character.SelectedCharacter = null;
            }
        }

        void UpdateSineAnim(float deltaTime)
        {
            if (CurrentSwimParams == null) { return; }
            movement = TargetMovement;

            if (movement.LengthSquared() > 0.00001f)
            {
                Collider.LinearVelocity = Vector2.Lerp(Collider.LinearVelocity, movement, 0.5f);
            }

            //limbs are disabled when simple physics is enabled, no need to move them
            if (SimplePhysicsEnabled) { return; }

            MainLimb.PullJointEnabled = true;
            //MainLimb.PullJointWorldAnchorB = Collider.SimPosition;

            if (movement.LengthSquared() < 0.00001f)
            {
                WalkPos = MathHelper.SmoothStep(WalkPos, MathHelper.PiOver2, deltaTime * 5);
                MainLimb.PullJointWorldAnchorB = Vector2.Lerp(MainLimb.PullJointWorldAnchorB, Collider.SimPosition, 0.5f);
                return;
            }

            float movementAngle = MathUtils.VectorToAngle(movement) - MathHelper.PiOver2;            
            if (CurrentSwimParams.RotateTowardsMovement)
            {
                Collider.SmoothRotate(movementAngle, CurrentSwimParams.SteerTorque);
                if (TorsoAngle.HasValue)
                {
                    Limb torso = GetLimb(LimbType.Torso);
                    torso?.body.SmoothRotate(movementAngle + TorsoAngle.Value * Dir, TorsoTorque);
                }
                if (HeadAngle.HasValue)
                {
                    Limb head = GetLimb(LimbType.Head);
                    head?.body.SmoothRotate(movementAngle + HeadAngle.Value * Dir, HeadTorque);
                }
                if (TailAngle.HasValue)
                {
                    Limb tail = GetLimb(LimbType.Tail);
                    tail?.body.SmoothRotate(movementAngle + TailAngle.Value * Dir, TailTorque);
                }
            }
            else
            {
                if (MainLimb.type == LimbType.Head && HeadAngle.HasValue)
                {
                    Collider.SmoothRotate(HeadAngle.Value * Dir, CurrentSwimParams.SteerTorque);
                }
                else if (MainLimb.type == LimbType.Torso && TorsoAngle.HasValue)
                {
                    Collider.SmoothRotate(TorsoAngle.Value * Dir, CurrentSwimParams.SteerTorque);
                }
                if (TorsoAngle.HasValue)
                {
                    Limb torso = GetLimb(LimbType.Torso);
                    torso?.body.SmoothRotate(TorsoAngle.Value * Dir, TorsoTorque);
                }
                if (HeadAngle.HasValue)
                {
                    Limb head = GetLimb(LimbType.Head);
                    head?.body.SmoothRotate(HeadAngle.Value * Dir, HeadTorque);
                }
                if (TailAngle.HasValue)
                {
                    Limb tail = GetLimb(LimbType.Tail);
                    tail?.body.SmoothRotate(TailAngle.Value * Dir, TailTorque);
                }
            }

            var waveLength = Math.Abs(CurrentSwimParams.WaveLength * RagdollParams.JointScale);
            var waveAmplitude = Math.Abs(CurrentSwimParams.WaveAmplitude);
            if (waveLength > 0 && waveAmplitude > 0)
            {
                WalkPos -= movement.Length() / Math.Abs(waveLength);
                WalkPos = MathUtils.WrapAngleTwoPi(WalkPos);
            }

            foreach (var limb in Limbs)
            {
                switch (limb.type)
                {
                    case LimbType.LeftFoot:
                    case LimbType.RightFoot:
                        if (CurrentSwimParams.FootAnglesInRadians.ContainsKey(limb.limbParams.ID))
                        {
                            limb.body.SmoothRotate(CurrentSwimParams.FootAnglesInRadians[limb.limbParams.ID] * Dir, FootTorque);
                        }
                        break;
                    case LimbType.Tail:
                        if (waveLength > 0 && waveAmplitude > 0)
                        {
                            float waveRotation = (float)Math.Sin(WalkPos);
                            limb.body.ApplyTorque(waveRotation * limb.Mass * CurrentSwimParams.TailTorque * waveAmplitude);
                        }
                        break;
                }
            }

            for (int i = 0; i < Limbs.Length; i++)
            {
                if (Limbs[i].SteerForce <= 0.0f) continue;

                Vector2 pullPos = Limbs[i].PullJointWorldAnchorA;
                Limbs[i].body.ApplyForce(movement * Limbs[i].SteerForce * Limbs[i].Mass, pullPos);
            }

            if (CurrentSwimParams.UseSineMovement)
            {
                MainLimb.PullJointWorldAnchorB = Vector2.SmoothStep(MainLimb.PullJointWorldAnchorB, Collider.SimPosition, (float)Math.Abs(Math.Sin(WalkPos)));
            }
            else
            {
                //MainLimb.PullJointWorldAnchorB = Collider.SimPosition;
                MainLimb.PullJointWorldAnchorB = Vector2.Lerp(MainLimb.PullJointWorldAnchorB, Collider.SimPosition, 0.5f);
            }

            floorY = Limbs[0].SimPosition.Y;            
        }
            
        void UpdateWalkAnim(float deltaTime)
        {
            if (CurrentGroundedParams == null) { return; }
            movement = MathUtils.SmoothStep(movement, TargetMovement, 0.2f);
            
            Collider.LinearVelocity = new Vector2(
                movement.X,
                Collider.LinearVelocity.Y > 0.0f ? Collider.LinearVelocity.Y * 0.5f : Collider.LinearVelocity.Y);

            //limbs are disabled when simple physics is enabled, no need to move them
            if (SimplePhysicsEnabled) { return; }

            float mainLimbHeight = ColliderHeightFromFloor;

            Vector2 colliderBottom = GetColliderBottom();

            Limb torso = GetLimb(LimbType.Torso);
            if (torso != null)
            {
                if (TorsoAngle.HasValue) torso.body.SmoothRotate(TorsoAngle.Value * Dir, TorsoTorque);
                if (TorsoPosition.HasValue)
                {
                    Vector2 pos = colliderBottom + Vector2.UnitY * TorsoPosition.Value;

                    if (torso != MainLimb)
                        pos.X = torso.SimPosition.X;
                    else
                        mainLimbHeight = TorsoPosition.Value;

                    torso.MoveToPos(pos, TorsoMoveForce);
                    torso.PullJointEnabled = true;
                    torso.PullJointWorldAnchorB = pos;
                }
            }

            Limb head = GetLimb(LimbType.Head);
            if (head != null)
            {
                if (HeadAngle.HasValue) head.body.SmoothRotate(HeadAngle.Value * Dir, HeadTorque);
                if (HeadPosition.HasValue)
                {
                    Vector2 pos = colliderBottom + Vector2.UnitY * HeadPosition.Value;

                    if (head != MainLimb)
                        pos.X = head.SimPosition.X;
                    else
                        mainLimbHeight = HeadPosition.Value;

                    head.MoveToPos(pos, HeadMoveForce);
                    head.PullJointEnabled = true;
                    head.PullJointWorldAnchorB = pos;
                }
            }

            if (TailAngle.HasValue)
            {
                GetLimb(LimbType.Tail)?.body.SmoothRotate(TailAngle.Value * Dir, TailTorque);
            }
                        
            WalkPos -= MainLimb.LinearVelocity.X * (CurrentAnimationParams.CycleSpeed / RagdollParams.JointScale / 100.0f);

            Vector2 transformedStepSize = Vector2.Zero;
            if (Math.Abs(TargetMovement.X) > 0.01f)
            {
                transformedStepSize = new Vector2(
                    (float)Math.Cos(WalkPos) * StepSize.Value.X * 3.0f,
                    (float)Math.Sin(WalkPos) * StepSize.Value.Y * 2.0f);
            }

            foreach (Limb limb in Limbs)
            {
                switch (limb.type)
                {
                    case LimbType.LeftFoot:
                    case LimbType.RightFoot:
                        Vector2 footPos = new Vector2(limb.SimPosition.X, colliderBottom.Y);

                        if (limb.RefJointIndex > -1)
                        {
                            if (LimbJoints.Length <= limb.RefJointIndex)
                            {
                                DebugConsole.ThrowError($"Reference joint index {limb.RefJointIndex} is out of array. This is probably due to a missing joint. If you just deleted a joint, don't do that without first removing the reference joint indices from the limbs. If this is not the case, please ensure that you have defined the index to the right joint.");
                            }
                            else
                            {
                                footPos.X = LimbJoints[limb.RefJointIndex].WorldAnchorA.X;
                            }
                        }
                        footPos.X += limb.StepOffset.X * Dir;
                        footPos.Y += limb.StepOffset.Y;

                        if (limb.type == LimbType.LeftFoot)
                        {
                            limb.DebugRefPos = footPos + Vector2.UnitX * movement.X * 0.1f;
                            limb.DebugTargetPos = footPos + new Vector2(
                                transformedStepSize.X + movement.X * 0.1f,
                                (transformedStepSize.Y > 0.0f) ? transformedStepSize.Y : 0.0f);
                            limb.MoveToPos(limb.DebugTargetPos, FootMoveForce);
                        }
                        else if (limb.type == LimbType.RightFoot)
                        {
                            limb.DebugRefPos = footPos + Vector2.UnitX * movement.X * 0.1f;
                            limb.DebugTargetPos = footPos + new Vector2(
                                -transformedStepSize.X + movement.X * 0.1f,
                                (-transformedStepSize.Y > 0.0f) ? -transformedStepSize.Y : 0.0f);
                            limb.MoveToPos(limb.DebugTargetPos, FootMoveForce);
                        }
                        
                        if (CurrentGroundedParams.FootAnglesInRadians.ContainsKey(limb.limbParams.ID))
                        {
                            limb.body.SmoothRotate(CurrentGroundedParams.FootAnglesInRadians[limb.limbParams.ID] * Dir, FootTorque);
                        }
     
                        break;
                    case LimbType.LeftLeg:
                    case LimbType.RightLeg:
                        if (Math.Abs(CurrentGroundedParams.LegTorque) > 0.001f) limb.body.ApplyTorque(limb.Mass * CurrentGroundedParams.LegTorque * Dir);
                        break;
                }
            }
        }
        
        void UpdateDying(float deltaTime)
        {
            if (deathAnimDuration <= 0.0f) return;

            float animStrength = (1.0f - deathAnimTimer / deathAnimDuration);

            Limb head = GetLimb(LimbType.Head);
            Limb tail = GetLimb(LimbType.Tail);

            if (head != null && !head.IsSevered) head.body.ApplyTorque((float)(Math.Sqrt(head.Mass) * Dir * Math.Sin(WalkPos)) * 30.0f * animStrength);
            if (tail != null && !tail.IsSevered) tail.body.ApplyTorque((float)(Math.Sqrt(tail.Mass) * -Dir * Math.Sin(WalkPos)) * 30.0f * animStrength);

            WalkPos += deltaTime * 10.0f * animStrength;

            Vector2 centerOfMass = GetCenterOfMass();

            foreach (Limb limb in Limbs)
            {
#if CLIENT
                if (limb.LightSource != null)
                {
                    limb.LightSource.Color = Color.Lerp(limb.InitialLightSourceColor, Color.TransparentBlack, deathAnimTimer / deathAnimDuration);
                }
#endif
                if (limb.type == LimbType.Head || limb.type == LimbType.Tail || limb.IsSevered || !limb.body.Enabled) continue;
                if (limb.Mass <= 0.0f)
                {
                    string errorMsg = "Creature death animation error: invalid limb mass on character \"" + character.SpeciesName + "\" (type: " + limb.type + ", mass: " + limb.Mass + ")";
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("FishAnimController.UpdateDying:InvalidMass" + character.ID, GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    deathAnimTimer = deathAnimDuration;
                    return;
                }

                Vector2 diff = (centerOfMass - limb.SimPosition);
                if (!MathUtils.IsValid(diff))
                {
                    string errorMsg = "Creature death animation error: invalid diff (center of mass: " + centerOfMass + ", limb position: " + limb.SimPosition + ")";
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("FishAnimController.UpdateDying:InvalidDiff" + character.ID, GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    deathAnimTimer = deathAnimDuration;
                    return;
                }

                limb.body.ApplyForce(diff * (float)(Math.Sin(WalkPos) * Math.Sqrt(limb.Mass)) * 30.0f * animStrength);
            }
        }

        public override void Flip()
        {
            base.Flip();
            foreach (Limb l in Limbs)
            {
                if (!l.DoesFlip) continue;
                l.body.SetTransform(l.SimPosition, -l.body.Rotation - MathHelper.ToRadians(RagdollParams.SpritesheetOrientation));                
            }
        }

        private void Mirror()
        {
            Vector2 centerOfMass = GetCenterOfMass();

            foreach (Limb l in Limbs)
            {
                TrySetLimbPosition(l,
                    centerOfMass,
                    new Vector2(centerOfMass.X - (l.SimPosition.X - centerOfMass.X), l.SimPosition.Y),
                    true);
            }
        }
  
    }
}
