﻿using Assets.Scripts.Characters.Titan.Attacks;
using Assets.Scripts.Characters.Titan.Behavior;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MonoBehaviour = Photon.MonoBehaviour;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Characters.Titan
{
    public class MindlessTitan : MonoBehaviour
    {
        public MindlessTitanState TitanState = MindlessTitanState.Wandering;
        public MindlessTitanState PreviousState;
        public MindlessTitanType Type;

        public bool IsAlive => TitanState != MindlessTitanState.Dead;
        private float DamageTimer { get; set; }
        public TitanBody TitanBody { get; private set; }
        public Animation Animation { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        private TitanBehavior[] Behaviors { get; set; }

        private string CurrentAnimation { get; set; } = "idle_2";
        private string AnimationTurnLeft { get; set; } = "turnaround2";
        private string AnimationTurnRight { get; set; } = "turnaround1";
        public string AnimationWalk { get; private set; } = "run_walk";
        private string AnimationRun { get; set; }
        private string AnimationRecovery { get; set; } = "tired";
        private string AnimationDeath { get; set; } = "die_back";

        private float turnDeg;
        private float desDeg;
        private int nextUpdate = 1;
        private float attackCooldown;
        private float staminaLimit;

        public float AttackDistance { get; protected set; }

        public float Speed = 10f;
        public float TargetDistance = 1f;
        public float Size = 3f;
        public float Stamina = 10f;
        public float StaminaRecovery = 1f;
        public int Health;
        public float ViewDistance;
        public float Focus = 10f;
        private float FocusTimer { get; set; }
        private int MaxHealth { get; set; }

        private bool isHooked;
        public bool IsHooked
        {
            get { return isHooked; }
            set
            {
                if (value == isHooked) return;
                isHooked = value;
                CheckColliders();
            }
        }

        private bool isLooked;
        public bool IsLooked
        {
            get { return isLooked; }
            set
            {
                if (value == isLooked) return;
                isLooked = value;
                CheckColliders();
            }
        }

        private bool isColliding;
        public bool IsColliding
        {
            get { return isColliding; }
            set
            {
                if (value == isColliding) return;
                isColliding = value;
                CheckColliders();
            }
        }

        public Hero Target { get; set; }
        private Hero GrabTarget { get; set; }
        public float RotationModifier { get; private set; }

        public List<Attack> Attacks { get; private set; }
        public Attack CurrentAttack { get; set; }
        private Collider[] Colliders { get; set; }
        private GameObject HealthLabel { get; set; }
        private FengGameManagerMKII GameManager { get; set; }

        void Awake()
        {
            GameManager = FengGameManagerMKII.instance;
            GameManager.addTitan(this);
            TitanBody = GetComponent<TitanBody>();
            Animation = GetComponent<Animation>();
            Rigidbody = GetComponent<Rigidbody>();
            this.oldHeadRotation = TitanBody.Head.rotation;
            this.grabTF = new GameObject();
            this.grabTF.name = "titansTmpGrabTF";
            Colliders = GetComponentsInChildren<Collider>().Where(x => x.name != "AABB")
                .ToArray();
            CheckColliders();

            GameObject obj2 = new GameObject
            {
                name = "PlayerCollisionDetection"
            };
            CapsuleCollider collider2 = obj2.AddComponent<CapsuleCollider>();
            CapsuleCollider component = transform.Find("AABB").GetComponent<CapsuleCollider>();
            collider2.center = component.center;
            collider2.radius = Math.Abs((float)(transform.Find("Amarture/Core/Controller_Body/hip/spine/chest/neck/head").position.y - transform.position.y));
            collider2.height = component.height * 1.2f;
            collider2.material = component.material;
            collider2.isTrigger = true;
            collider2.name = "PlayerCollisionDetection";
            obj2.AddComponent<TitanTrigger>();
            obj2.layer = 0x10;
            obj2.transform.parent = this.transform.Find("AABB");
            obj2.transform.localPosition = new Vector3(0f, 0f, 0f);
        }

        public bool IsDisabled(params BodyPart[] bodyParts)
        {
            if (bodyParts == null) return false;
            return bodyParts.All(bodyPart => TitanBody.GetDisabledBodyParts().Contains(bodyPart));
        }

        public void Initialize(TitanConfiguration configuration)
        {
            Health = configuration.Health;
            MaxHealth = Health;
            Speed = configuration.Speed;
            Attacks = configuration.Attacks;
            Size = configuration.Size;
            ViewDistance = configuration.ViewDistance;
            AnimationWalk = configuration.AnimationWalk;
            AnimationRun = configuration.AnimationRun;
            AnimationDeath = configuration.AnimationDeath;
            AnimationRecovery = configuration.AnimationRecovery;
            AnimationTurnLeft = configuration.AnimationTurnLeft;
            AnimationTurnRight = configuration.AnimationTurnRight;
            Stamina = configuration.Stamina;
            StaminaRecovery = configuration.StaminaRegeneration;
            staminaLimit = Stamina;
            Focus = configuration.Focus;
            FocusTimer = 0f;
            Behaviors = configuration.Behaviors.ToArray();
            foreach (var behavior in Behaviors)
            {
                behavior.Initialize(this);
            }
            Type = configuration.Type;
            name = Type.ToString();

            transform.localScale = new Vector3(Size, Size, Size);
            var scale = Mathf.Min(Mathf.Pow(2f / Size, 0.35f), 1.25f);
            headscale = new Vector3(scale, scale, scale);
            TitanBody.Head.localScale = headscale;
            //TODO: Not working
            //LoadSkin();

            AttackDistance = Vector3.Distance(base.transform.position, TitanBody.AttackFrontGround.position) * 1.65f;

            if (Health > 0)
            {
                HealthLabel = (GameObject)Instantiate(Resources.Load("UI/LabelNameOverHead"));
                HealthLabel.name = "HealthLabel";
                HealthLabel.transform.parent = base.transform;
                HealthLabel.transform.localPosition = new Vector3(0f, 20f + (1f / Size), 0f);
                if (Type == MindlessTitanType.Crawler)
                {
                    HealthLabel.transform.localPosition = new Vector3(0f, 10f + (1f / Size), 0f);
                }
                float x = 1f;
                if (Size < 1f)
                {
                    x = 1f / Size;
                }

                x *= 0.08f;
                HealthLabel.transform.localScale = new Vector3(x, x, x);
            }

            if (photonView.isMine)
            {
                configuration.Behaviors = new List<TitanBehavior>();
                var config = JsonConvert.SerializeObject(configuration);
                photonView.RPC("InitializeRpc", PhotonTargets.OthersBuffered, config);

                if (Health > 0)
                {
                    photonView.RPC("UpdateHealthLabelRpc", PhotonTargets.AllBuffered, Health, MaxHealth);
                }
            }
        }

        [PunRPC]
        public void InitializeRpc(string titanConfiguration, PhotonMessageInfo info)
        {
            if (info.sender.ID == photonView.ownerId)
            {
                Initialize(JsonConvert.DeserializeObject<TitanConfiguration>(titanConfiguration));
            }
        }

        private void LoadSkin()
        {
            var eye = false;
            if (!(((IN_GAME_MAIN_CAMERA.gametype == GAMETYPE.SINGLE) || base.photonView.isMine) ? (((int)FengGameManagerMKII.settings[1]) != 1) : true))
            {
                int index = (int)UnityEngine.Random.Range((float)86f, (float)90f);
                int num2 = index - 60;
                if (((int)FengGameManagerMKII.settings[0x20]) == 1)
                {
                    num2 = UnityEngine.Random.Range(0x1a, 30);
                }
                string body = (string)FengGameManagerMKII.settings[index];
                string eyes = (string)FengGameManagerMKII.settings[num2];
                var skin = index;
                if ((eyes.EndsWith(".jpg") || eyes.EndsWith(".png")) || eyes.EndsWith(".jpeg"))
                {
                    eye = true;
                }
                base.GetComponent<TITAN_SETUP>().setVar(skin, eye);
            }
            GetComponent<TITAN_SETUP>().setHair2();
        }

        private bool asClientLookTarget;
        private Quaternion oldHeadRotation;
        private Quaternion targetHeadRotation;
        private Vector3 headscale;

        [PunRPC]
        private void setIfLookTarget(bool bo)
        {
            this.asClientLookTarget = bo;
        }

        public GameObject grabTF { get; set; }

        [PunRPC]
        public void Grab(bool isLeftHand)
        {
            var hand = isLeftHand ? TitanBody.HandLeft : TitanBody.HandRight;
            this.grabTF.transform.parent = hand;
            this.grabTF.transform.position = hand.GetComponent<SphereCollider>().transform.position;
            this.grabTF.transform.rotation = hand.GetComponent<SphereCollider>().transform.rotation;
            Transform transform1 = this.grabTF.transform;
            transform1.localPosition -= (Vector3)((Vector3.right * hand.GetComponent<SphereCollider>().radius) * 0.3f);
            Transform transform2 = this.grabTF.transform;
            if (isLeftHand)
            {
                transform2.localPosition -= (Vector3)((Vector3.up * hand.GetComponent<SphereCollider>().radius) * 0.51f);
            }
            else
            {
                transform2.localPosition += (Vector3)((Vector3.up * hand.GetComponent<SphereCollider>().radius) * 0.51f);
            }

            Transform transform3 = this.grabTF.transform;
            transform3.localPosition -= (Vector3)((Vector3.forward * hand.GetComponent<SphereCollider>().radius) * 0.3f);

            float zRotation = isLeftHand ? 180f : 0f;
            this.grabTF.transform.localRotation = Quaternion.Euler(this.grabTF.transform.localRotation.eulerAngles.x, this.grabTF.transform.localRotation.eulerAngles.y + 180f, this.grabTF.transform.localRotation.eulerAngles.z + zRotation);
        }

        [PunRPC]
        public void GrabEscapeRpc()
        {
            GrabTarget = null;
        }

        private void justEatHero(Hero grabTarget)
        {
            if (grabTarget != null)
            {
                if ((IN_GAME_MAIN_CAMERA.gametype == GAMETYPE.MULTIPLAYER) && base.photonView.isMine)
                {
                    if (!grabTarget.HasDied())
                    {
                        grabTarget.markDie();
                        object[] objArray2 = new object[] { -1, base.name };
                        grabTarget.photonView.RPC("netDie2", PhotonTargets.All, objArray2);
                    }
                }
                else if (IN_GAME_MAIN_CAMERA.gametype == GAMETYPE.SINGLE)
                {
                    grabTarget.die2(null);
                }
            }
        }

        private void HeadMovement()
        {
            if (TitanState != MindlessTitanState.Dead)
            {

                if (IN_GAME_MAIN_CAMERA.gametype != GAMETYPE.SINGLE)
                {
                    if (base.photonView.isMine)
                    {
                        targetHeadRotation = TitanBody.Head.rotation;
                        bool flag2 = false;
                        if (TitanState == MindlessTitanState.Chase && ((TargetDistance < 100f) && (Target != null)))
                        {
                            Vector3 vector = Target.transform.position - transform.position;
                            var angle = -Mathf.Atan2(vector.z, vector.x) * 57.29578f;
                            float num = -Mathf.DeltaAngle(angle, base.transform.rotation.eulerAngles.y - 90f);
                            num = Mathf.Clamp(num, -40f, 40f);
                            float y = (TitanBody.Neck.position.y + (Size * 2f)) - Target.transform.position.y;
                            float num3 = Mathf.Atan2(y, TargetDistance) * 57.29578f;
                            num3 = Mathf.Clamp(num3, -40f, 30f);
                            targetHeadRotation = Quaternion.Euler(TitanBody.Head.rotation.eulerAngles.x + num3, TitanBody.Head.rotation.eulerAngles.y + num, TitanBody.Head.rotation.eulerAngles.z);
                            if (!this.asClientLookTarget)
                            {
                                this.asClientLookTarget = true;
                                object[] parameters = new object[] { true };
                                base.photonView.RPC("setIfLookTarget", PhotonTargets.Others, parameters);
                            }
                            flag2 = true;
                        }
                        if (!(flag2 || !this.asClientLookTarget))
                        {
                            this.asClientLookTarget = false;
                            object[] objArray3 = new object[] { false };
                            base.photonView.RPC("setIfLookTarget", PhotonTargets.Others, objArray3);
                        }
                        if (TitanState == MindlessTitanState.Attacking)
                        {
                            oldHeadRotation = Quaternion.Lerp(oldHeadRotation, targetHeadRotation, Time.deltaTime * 20f);
                        }
                        else
                        {
                            oldHeadRotation = Quaternion.Lerp(oldHeadRotation, targetHeadRotation, Time.deltaTime * 10f);
                        }
                    }
                    else
                    {
                        var hasTarget = Target != null;
                        if (hasTarget)
                        {
                            TargetDistance = Mathf.Sqrt(((Target.transform.position.x - transform.position.x) * (Target.transform.position.x - transform.position.x)) + ((Target.transform.position.z - transform.position.z) * (Target.transform.position.z - transform.position.z)));
                        }
                        else
                        {
                            TargetDistance = float.MaxValue;
                        }
                        this.targetHeadRotation = TitanBody.Head.rotation;
                        if ((this.asClientLookTarget && hasTarget) && (TargetDistance < 100f))
                        {
                            Vector3 vector2 = Target.transform.position - transform.position;
                            var angle = -Mathf.Atan2(vector2.z, vector2.x) * 57.29578f;
                            float num4 = -Mathf.DeltaAngle(angle, transform.rotation.eulerAngles.y - 90f);
                            num4 = Mathf.Clamp(num4, -40f, 40f);
                            float num5 = (TitanBody.Neck.position.y + (Size * 2f)) - Target.transform.position.y;
                            float num6 = Mathf.Atan2(num5, TargetDistance) * 57.29578f;
                            num6 = Mathf.Clamp(num6, -40f, 30f);
                            this.targetHeadRotation = Quaternion.Euler(TitanBody.Head.rotation.eulerAngles.x + num6, TitanBody.Head.rotation.eulerAngles.y + num4, TitanBody.Head.rotation.eulerAngles.z);
                        }
                        this.oldHeadRotation = Quaternion.Slerp(this.oldHeadRotation, this.targetHeadRotation, Time.deltaTime * 10f);
                    }
                }
                TitanBody.Head.rotation = this.oldHeadRotation;
            }
            if (!base.GetComponent<Animation>().IsPlaying("die_headOff"))
            {
                TitanBody.Head.localScale = this.headscale;
            }
        }

        [PunRPC]
        protected void UpdateHealthLabelRpc(int currentHealth, int maxHealth)
        {
            if (currentHealth < 0)
            {
                if (HealthLabel != null)
                {
                    Destroy(HealthLabel);
                }
            }
            else
            {
                var color = "7FFF00";
                var num2 = ((float)currentHealth) / ((float)maxHealth);
                if ((num2 < 0.75f) && (num2 >= 0.5f))
                {
                    color = "f2b50f";
                }
                else if ((num2 < 0.5f) && (num2 >= 0.25f))
                {
                    color = "ff8100";
                }
                else if (num2 < 0.25f)
                {
                    color = "ff3333";
                }
                HealthLabel.GetComponent<TextMesh>().text = $"<color=#{color}>{currentHealth}</color>";
            }
        }

        [PunRPC]
        public void OnNapeHitRpc(int viewId, int damage)
        {
            if (!IsAlive) return;
            var view = PhotonView.Find(viewId);
            if (view == null || !IsAlive && Time.time - DamageTimer > 0.2f) return;
            DamageTimer = Time.time;

            Health -= damage;

            if (MaxHealth > 0)
            {
                photonView.RPC("UpdateHealthLabelRpc", PhotonTargets.AllBuffered, Health, MaxHealth);
            }

            if (Health <= 0)
            {
                Health = 0;
            }
            else
            {
                return;
            }

            OnTitanDeath();
            ChangeState(MindlessTitanState.Dead);
            FengGameManagerMKII.instance.titanGetKill(view.owner, damage, name);
        }

        [PunRPC]
        public void OnBodyPartHitRpc(BodyPart bodyPart, int damage)
        {
            if (!photonView.isMine) return;
            TitanBody.AddBodyPart(bodyPart);
            if (TitanBody.GetDisabledBodyParts().Any(x => x == BodyPart.LegLeft) 
                && TitanBody.GetDisabledBodyParts().Any(x => x == BodyPart.LegRight))
            {
                ChangeState(MindlessTitanState.Disabled);
            }
        }
        
        public void OnEyeHit(int viewId, int damage)
        {
            //if (this.state != TitanState.hit_eye)
            //{
            //    if ((this.state != TitanState.down) && (this.state != TitanState.sit))
            //    {
            //        this.playAnimation("hit_eye");
            //    }
            //    else
            //    {
            //        this.playAnimation("sit_hit_eye");
            //    }
            //    this.state = TitanState.hit_eye;
            //}
        }

        public void OnAnkleHit(int viewId, int damage)
        {
            return;
            var view = PhotonView.Find(viewId);
            if (view == null || !IsAlive && Time.time - DamageTimer < 0.2f) return;
            DamageTimer = Time.time;
            FengGameManagerMKII.instance.titanGetKill(view.owner, damage, "Titan Ankle");
        }

        public void OnTargetGrabbed(GameObject target, bool isLeftHand)
        {
            ChangeState(MindlessTitanState.Eat);
            GrabTarget = target.GetComponent<Hero>();
            if (isLeftHand)
            {
                CurrentAnimation = "eat_l";
            }
            else
            {
                CurrentAnimation = "eat_r";
            }
        }

        private void ReleaseGrabbedTarget()
        {
            if (GrabTarget != null)
            {
                GrabTarget.photonView.RPC("netUngrabbed", PhotonTargets.All);
            }
        }

        private void OnTitanDeath()
        {
            ReleaseGrabbedTarget();
        }


        private bool HasDieSteam { get; set; }
        private void Dead()
        {
            if (!Animation.IsPlaying(AnimationDeath))
            {
                CrossFade(AnimationDeath, 0.05f);
            }
            var deathTime = Animation[AnimationDeath].normalizedTime;
            if (deathTime > 2f && !HasDieSteam && photonView.isMine)
            {
                HasDieSteam = true;
                PhotonNetwork.Instantiate("FX/FXtitanDie1", TitanBody.Hip.position, Quaternion.Euler(-90f, 0f, 0f), 0).transform.localScale = transform.localScale;
            }
            if (deathTime > 5f)
            {
                if (base.photonView.isMine)
                {
                    PhotonNetwork.Instantiate("FX/FXtitanDie", TitanBody.Hip.position, Quaternion.Euler(-90f, 0f, 0f), 0).transform.localScale = transform.localScale;
                    PhotonNetwork.Destroy(gameObject);
                }
            }
        }

        public bool HasTarget()
        {
            return Target != null;
        }

        public void OnTargetDetected(GameObject target)
        {
            Target = target.GetComponent<Hero>();
            ChangeState(MindlessTitanState.Chase);
            FocusTimer = 0f;
            this.oldHeadRotation = TitanBody.Head.rotation;
        }

        public void ChangeState(MindlessTitanState state)
        {
            if (!IsAlive) return;
            if (state == TitanState) return;
            PreviousState = TitanState;
            TitanState = state;
        }

        private void RefreshStamina()
        {
            if (Stamina >= staminaLimit) return;
            Stamina += StaminaRecovery * Time.deltaTime;
            if (Stamina > staminaLimit)
            {
                Stamina = staminaLimit;
            }
        }

        private void CalculateTargetDistance()
        {
            if (Target == null) return;
            TargetDistance = Mathf.Sqrt(((Target.transform.position.x - transform.position.x) * (Target.transform.position.x - transform.position.x)) + ((Target.transform.position.z - transform.position.z) * (Target.transform.position.z - transform.position.z)));
        }

        private void Turn(float degrees)
        {
            ChangeState(MindlessTitanState.Turning);
            CurrentAnimation = degrees > 0f ? AnimationTurnLeft : AnimationTurnRight;
            CrossFade(CurrentAnimation, 0.1f);
            this.turnDeg = degrees;
            this.desDeg = base.gameObject.transform.rotation.eulerAngles.y + this.turnDeg;
        }

        private bool Between(float value, float min = -1f, float max = 1f)
        {
            return value > min && value < max;
        }

        public bool IsStuck()
        {
            var velocity = Rigidbody.velocity;
            return Between(velocity.z, -Speed / 4, Speed / 4) 
                   && Between(velocity.x, -Speed / 4, Speed / 4) 
                   && Animation[CurrentAnimation].normalizedTime > 2f;
        }

        void LateUpdate()
        {
            if (Target == null && TitanState == MindlessTitanState.Attacking)
            {
                ChangeState(MindlessTitanState.Wandering);
            }

            HeadMovement();

        }

        public void CrossFade(string newAnimation, float fadeLength, PhotonMessageInfo info = new PhotonMessageInfo())
        {
            if (PhotonNetwork.isMasterClient)
            {
                CurrentAnimation = newAnimation;
                Animation.CrossFade(newAnimation, fadeLength);
                photonView.RPC("CrossFadeRpc", PhotonTargets.Others, newAnimation, fadeLength);
            }
        }

        [PunRPC]
        private void CrossFadeRpc(string newAnimation, float fadeLength,
            PhotonMessageInfo info = new PhotonMessageInfo())
        {
            if (info.sender.IsMasterClient)
            {
                CurrentAnimation = newAnimation;
                Animation.CrossFade(newAnimation, fadeLength);
            }
        }

        private void CheckColliders()
        {
            if (!IsHooked && !IsLooked && !IsColliding)
            {
                foreach (Collider collider in Colliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                }
            }
            else if (IsHooked || IsLooked || IsColliding)
            {
                foreach (Collider collider in Colliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = true;
                    }
                }
            }
        }

        private bool CanRun()
        {
            if (AnimationRun == null) return false;
            if (TitanState == MindlessTitanState.Wandering)
            {
                return Stamina > staminaLimit / 2;
            }

            return true;
        }

        private void OnTargetRefresh()
        {
            FocusTimer = 0f;
            var positiveInfinity = float.PositiveInfinity;
            var position = this.transform.position;
            foreach (Hero hero in FengGameManagerMKII.instance.getPlayers())
            {
                var num2 = Vector3.Distance(hero.gameObject.transform.position, position);
                if (num2 < positiveInfinity)
                {
                    Target = hero;
                    positiveInfinity = num2;
                }
            }
        }

        private void OnDestroy()
        {
            FengGameManagerMKII.instance.removeTitan(this);
        }

        void Update()
        {
            if (!photonView.isMine) return;

            if (!IsAlive)
            {
                Dead();
                return;
            }

            RefreshStamina();
            CalculateTargetDistance();

            if (Time.time >= nextUpdate)
            {
                nextUpdate = Mathf.FloorToInt(Time.time) + 1;
                UpdateEverySecond(nextUpdate);
            }

            if (Stamina < 0 && TitanState != MindlessTitanState.Recovering && TitanState != MindlessTitanState.Disabled)
            {
                ChangeState(MindlessTitanState.Recovering);
            }

            if (Behaviors != null && Behaviors.Any(x => x.OnUpdate()))
            {
                return;
            }

            if (TitanState == MindlessTitanState.Disabled)
            {
                var disabledBodyParts = TitanBody.GetDisabledBodyParts();
                if (disabledBodyParts.Any(x => x == BodyPart.LegLeft)
                    && disabledBodyParts.Any(x => x == BodyPart.LegRight))
                {
                    CurrentAnimation = "attack_abnormal_jump";
                    if (!Animation.IsPlaying(CurrentAnimation))
                        CrossFade(CurrentAnimation, 0.1f);
                }
                else
                {
                    ChangeState(PreviousState);
                }
            }

            if (TitanState == MindlessTitanState.Recovering)
            {
                Stamina += Time.deltaTime * StaminaRecovery * 3f;
                CurrentAnimation = AnimationRecovery;
                if (!Animation.IsPlaying(CurrentAnimation))
                {
                    CrossFade(CurrentAnimation, 0.1f);
                }

                if (Stamina > staminaLimit * 0.75f)
                {
                    ChangeState(MindlessTitanState.Chase);
                }
            }

            if (TitanState == MindlessTitanState.Wandering)
            {
                CurrentAnimation = AnimationWalk;
                if (!Animation.IsPlaying(CurrentAnimation))
                {
                    CrossFade(CurrentAnimation, 0.5f);
                }
                return;
            }

            if (TitanState == MindlessTitanState.Turning)
            {
                gameObject.transform.rotation = Quaternion.Lerp(gameObject.transform.rotation, Quaternion.Euler(0f, this.desDeg, 0f), (Time.deltaTime * Mathf.Abs(this.turnDeg)) * 0.015f);
                if (Animation[CurrentAnimation].normalizedTime > 1f)
                {
                    ChangeState(PreviousState);
                }

                return;
            }

            if (TitanState == MindlessTitanState.Chase)
            {
                if (Target == null || ViewDistance < TargetDistance)
                {
                    ChangeState(MindlessTitanState.Wandering);
                    return;
                }

                CurrentAnimation = AnimationWalk;
                if (CanRun())
                {
                    CurrentAnimation = AnimationRun;
                    Stamina -= Time.deltaTime * 2;
                }

                FocusTimer += Time.deltaTime;
                if (FocusTimer > Focus)
                {
                    OnTargetRefresh();
                }

                if (!Animation.IsPlaying(CurrentAnimation))
                {
                    CrossFade(CurrentAnimation, 0.1f);
                    return;
                }

                if (attackCooldown > 0)
                {
                    attackCooldown -= Time.deltaTime * CurrentAttack.Cooldown;
                    return;
                }

                var availableAttacks = Attacks.Where(x => x.CanAttack(this));
                CurrentAttack = availableAttacks.FirstOrDefault();
                if (CurrentAttack != null)
                {
                    ChangeState(MindlessTitanState.Attacking);
                }
                else
                {
                    Vector3 vector18 = Target.transform.position - transform.position;
                    var angle = -Mathf.Atan2(vector18.z, vector18.x) * 57.29578f;
                    var between = -Mathf.DeltaAngle(angle, gameObject.transform.rotation.eulerAngles.y - 90f);
                    if (Mathf.Abs(between) > 45f && Vector3.Distance(Target.transform.position, transform.position) < 50f * Size)
                    {
                        Turn(between);
                        return;
                    }
                }
                return;
            }

            if (TitanState == MindlessTitanState.Attacking)
            {
                if (CurrentAttack.IsFinished)
                {
                    CurrentAttack.IsFinished = false;
                    Stamina -= 10f;
                    ChangeState(MindlessTitanState.Chase);
                    return;
                }
                CurrentAttack.Execute(this);
            }

            if (TitanState == MindlessTitanState.Eat)
            {
                if (!Animation.IsPlaying(CurrentAnimation))
                {
                    CrossFade(CurrentAnimation, 0.1f);
                    return;
                }

                if (Animation[CurrentAnimation].normalizedTime >= 0.48f && GrabTarget != null)
                {
                    this.justEatHero(GrabTarget);
                }

                if (Animation[CurrentAnimation].normalizedTime > 1f)
                {
                    ChangeState(PreviousState);
                }
            }
        }

        private bool Pathfinding()
        {
            Vector3 forwardDirection = TitanBody.Hip.transform.TransformDirection(new Vector3(-0.3f, 0, 1f));
            RaycastHit objectHit;
            var mask = ~LayerMask.NameToLayer("Ground");
            if (Physics.Raycast(TitanBody.Hip.transform.position, forwardDirection, out objectHit, 50, mask))
            {
                Vector3 leftDirection = TitanBody.Hip.transform.TransformDirection(new Vector3(-0.3f, -1f, 1f));
                Vector3 rightDirection = TitanBody.Hip.transform.TransformDirection(new Vector3(-0.3f, 1f, 1f));
                RaycastHit leftHit;
                RaycastHit rightHit;
                Physics.Raycast(TitanBody.Hip.transform.position, leftDirection, out leftHit, 250 , mask);
                Physics.Raycast(TitanBody.Hip.transform.position, rightDirection, out rightHit, 250, mask);

                if (leftHit.distance < rightHit.distance)
                {
                    //transform.Rotate(0, 15f, 0);
                    RotationModifier += Random.Range(10f, 30f);
                }
                else
                {
                    //transform.Rotate(0, -15f, 0);
                    RotationModifier += Random.Range(-30f, -10f); ;
                }

                return true;
            }

            RotationModifier = 0f;
            return false;
        }

        private bool IsPathFinding { get; set; }
        void UpdateEverySecond(int seconds)
        {
            if (Behaviors != null && Behaviors.Any(x => x.OnUpdateEverySecond(seconds)))
            {
                return;
            }

            //if (TitanState == MindlessTitanState.Wandering || TitanState == MindlessTitanState.Chase && Type != MindlessTitanType.Crawler)
            //{
            //    IsPathFinding = Pathfinding();
            //}
            //else
            //{
            //    RotationModifier = 0f;
            //}
        }

        void FixedUpdate()
        {
            if (!photonView.isMine) return;
            Rigidbody.AddForce(new Vector3(0f, -120f * Rigidbody.mass, 0f));
            if (Behaviors != null && Behaviors.Any(x => x.OnFixedUpdate()))
            {
                return;
            }

            if (TitanState == MindlessTitanState.Wandering)
            {
                if (IsStuck())
                {
                    Turn(Random.Range(-270, 270));
                    return;
                }

                var runModifier = 1f;
                Vector3 vector12 = transform.forward * Speed * runModifier;
                Vector3 vector14 = vector12 - Rigidbody.velocity;
                vector14.x = Mathf.Clamp(vector14.x, -10f, 10f);
                vector14.z = Mathf.Clamp(vector14.z, -10f, 10f);
                vector14.y = 0f;
                Rigidbody.AddForce(vector14, ForceMode.VelocityChange);
                transform.Rotate(0, RotationModifier * Time.fixedDeltaTime, 0);
            }

            if (TitanState == MindlessTitanState.Chase)
            {
                if (Target == null) return;
                var runModifier = CanRun() ? 1.25f : 1f;
                Vector3 vector12 = transform.forward * Speed * runModifier;
                Vector3 vector14 = vector12 - Rigidbody.velocity;
                vector14.x = Mathf.Clamp(vector14.x, -10f, 10f);
                vector14.z = Mathf.Clamp(vector14.z, -10f, 10f);
                vector14.y = 0f;
                Rigidbody.AddForce(vector14, ForceMode.VelocityChange);
                Vector3 vector17 = Target.transform.position - transform.position;
                var current = -Mathf.Atan2(vector17.z, vector17.x) * 57.29578f + RotationModifier;
                float num4 = -Mathf.DeltaAngle(current, transform.rotation.eulerAngles.y - 90f);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, transform.rotation.eulerAngles.y + num4, 0f), ((Speed * 0.5f) * Time.deltaTime) / Size);
            }
        }
    }
}
