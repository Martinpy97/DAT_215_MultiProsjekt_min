using System.Collections;
using System.Collections.Generic;
using SparkBossFramework001.Core;
using SparkBossFramework001.Sandbox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SparkBossFramework001.Unity
{
    // Fake encounter only, never production Combat.
    public sealed class BossSandboxPlayable : MonoBehaviour
    {
        public BossSandboxRunner runner;
        public Transform player;
        public Camera arenaCamera;
        public int PlayerHealth { get; private set; } = 100;
        public int RuneCount { get; private set; }
        public bool Ready { get; private set; }
        public bool Won => Ready && runner.Authority.Runtime.Snapshot().Lifecycle == BossLifecycleState.Defeated;
        public bool Lost => PlayerHealth <= 0;
        public int AddHealth => _combat == null ? 0 : _combat.AddHealth;
        public bool RangedImmune => _combat != null && _combat.RangedImmune;
        public Vector3 AddPosition => _visual.Add.position;
        public int ProjectileCount => _shots.Count;
        public string Message { get; private set; } = "Bla energilinje = avstandsskjold. Ta skjoldvokteren forst!";
        private Transform Boss => runner.bossVisual.transform;
        private BossPhaseId Phase => runner.Authority.Runtime.Snapshot().CurrentPhaseId;
        private bool Shielded => Phase.Equals(FakeEncounterAuthority.P2);
        private SandboxCombatFixture _combat;
        private BossSandboxVisuals _visual;
        private readonly bool[] _runes = new bool[3];
        private readonly List<Shot> _shots = new List<Shot>();
        private readonly List<Feedback> _feedback = new List<Feedback>();
        private readonly List<Effect> _effects = new List<Effect>();
        private Vector3 _spawn, _cameraHome, _bossScale, _aimPoint, _direction=Vector3.forward, _dashDirection;
        private float _clock, _nextAttack, _nextShot, _nextDash, _dashUntil, _nextPulse=3, _pulseAt=-1, _pulseRadius, _flashUntil, _swingUntil, _nextAddAttack=2, _shakeUntil, _playerFlash;
        private bool _paused;
        private GUIStyle _title, _body, _small, _number, _center;
        private static readonly Color Cyan=new Color(.15f,.86f,1f), Gold=new Color(1f,.72f,.22f), Red=new Color(1f,.3f,.2f), Green=new Color(.3f,1f,.65f);
        private sealed class Shot { public Transform visual; public Vector3 velocity; public float expires; public BossAttemptScope scope; }
        private sealed class Feedback { public Vector3 position; public string text; public Color color; public float until; }
        private sealed class Effect { public Transform visual; public float until; }

        private IEnumerator Start()
        {
            if(runner==null || player==null || arenaCamera==null){Debug.LogError("Playable sandbox references missing");enabled=false;yield break;}
            while(runner.Authority==null)yield return null;
            _spawn=player.position;_cameraHome=arenaCamera.transform.position;_bossScale=Boss.localScale;
            _combat=new SandboxCombatFixture(runner.Authority);_visual=new BossSandboxVisuals(transform,Boss,player);
            _aimPoint=Boss.position;Ready=true;RefreshVisuals();
        }
        private void Update()
        {
            if(!Ready)return;
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null && k.rKey.wasPressedThisFrame)Restart();
            if(k!=null && k.escapeKey.wasPressedThisFrame)_paused=!_paused;
            if(_paused || !Application.isFocused)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);
            if(!Won && !Lost)
            {
                if(m!=null){var ray=arenaCamera.ScreenPointToRay(m.position.ReadValue());float distance;if(new Plane(Vector3.up,new Vector3(0,1,0)).Raycast(ray,out distance))AimAt(ray.GetPoint(distance));}
                Vector2 move=Vector2.zero;
                if(k!=null)
                {
                    move.x=((k.dKey.isPressed||k.rightArrowKey.isPressed)?1:0)-((k.aKey.isPressed||k.leftArrowKey.isPressed)?1:0);
                    move.y=((k.wKey.isPressed||k.upArrowKey.isPressed)?1:0)-((k.sKey.isPressed||k.downArrowKey.isPressed)?1:0);
                    if(k.spaceKey.wasPressedThisFrame)Dash();if(k.eKey.wasPressedThisFrame)Interact();
                }
                Move(move,dt);
                if((k!=null&&k.jKey.isPressed)||(m!=null&&m.leftButton.isPressed))Attack();
                if((k!=null&&k.kKey.isPressed)||(m!=null&&m.rightButton.isPressed))Shoot();
            }
            Simulate(dt);
        }
        // Tests advance this clock directly rather than relying on editor wall time.
        public void Simulate(float deltaTime)
        {
            if(!Ready || _paused)return;
            float dt=Mathf.Clamp(deltaTime,0,.05f);_clock+=dt;
            if(!Won && !Lost){AdvanceAdd(dt);AdvanceProjectiles(dt);if(!Won)AdvanceHazard();}
            for(int i=_feedback.Count-1;i>=0;i--)if(_clock>=_feedback[i].until)_feedback.RemoveAt(i);
            for(int i=_effects.Count-1;i>=0;i--){var e=_effects[i];if(_clock>=e.until){Destroy(e.visual.gameObject);_effects.RemoveAt(i);}else e.visual.localScale=Vector3.one*(1+(.35f-(e.until-_clock))*6);}
            RefreshVisuals();
        }
        public void AimAt(Vector3 point)
        {
            if(!Ready)return;point.y=1.1f;
            if(RangedImmune && FlatDistance(point,AddPosition)<1.6f)point=AddPosition;
            else if(FlatDistance(point,Boss.position)<1.8f)point=Boss.position;
            _aimPoint=point;
        }
        public void Move(Vector2 input,float dt)
        {
            if(!Ready||Won||Lost||_paused)return;
            Vector3 f=arenaCamera.transform.forward;f.y=0;f.Normalize();Vector3 r=arenaCamera.transform.right;r.y=0;r.Normalize();
            var motion=Vector3.ClampMagnitude(r*input.x+f*input.y,1);if(motion.sqrMagnitude>.01f)_direction=motion.normalized;
            var pos=player.position+(_clock<_dashUntil?_dashDirection*13:motion*5)*Mathf.Clamp(dt,0,.2f);
            pos.x=Mathf.Clamp(pos.x,-6.1f,6.1f);pos.z=Mathf.Clamp(pos.z,-6.1f,6.1f);
            var off=pos-Boss.position;off.y=0;if(off.sqrMagnitude<2.25f){if(off.sqrMagnitude<.001f)off=Vector3.back;pos=Boss.position+off.normalized*1.5f;}
            pos.y=_spawn.y;player.position=pos;
            var facing=_aimPoint-player.position;facing.y=0;if(facing.sqrMagnitude>.01f)player.rotation=Quaternion.LookRotation(facing);
        }
        public void Dash()
        {
            if(!Ready||Won||Lost||_paused||_clock<_nextDash)return;
            _dashDirection=_direction;_dashUntil=_clock+.2f;_nextDash=_clock+1.1f;Burst(player.position,Green);
        }
        public void Attack()
        {
            if(!Ready||Won||Lost||_paused||_clock<_nextAttack)return;
            _nextAttack=_clock+.36f;
            bool add=RangedImmune && FlatDistance(player.position,AddPosition)<FlatDistance(player.position,Boss.position);
            Vector3 target=add?AddPosition:Boss.position;var direction=target-player.position;direction.y=0;
            if(direction.sqrMagnitude<.01f)direction=_direction;
            _visual.Swing.position=player.position+direction.normalized*.9f;_visual.Swing.rotation=Quaternion.LookRotation(direction);
            _visual.Swing.localScale=new Vector3(1.7f,.13f,.4f);_swingUntil=_clock+.13f;
            if(FlatDistance(player.position,target)>2.85f){Message="For langt unna for naerkamp. Bruk hoyre mus for a skyte.";return;}
            ResolveHit(add,false,runner.Authority.Scope);
        }
        public void Shoot()
        {
            if(!Ready||Won||Lost||_paused||_clock<_nextShot||_shots.Count>=24)return;
            _nextShot=_clock+.38f;var direction=_aimPoint-player.position;direction.y=0;if(direction.sqrMagnitude<.01f)direction=_direction;
            direction.Normalize();var origin=player.position+direction*.65f+Vector3.up*.1f;
            _shots.Add(new Shot{visual=_visual.Projectile(origin),velocity=direction*18,expires=_clock+1.3f,scope=runner.Authority.Scope});
        }
        private void AdvanceProjectiles(float dt)
        {
            for(int i=_shots.Count-1;i>=0;i--)
            {
                var shot=_shots[i];var from=shot.visual.position;var to=from+shot.velocity*dt;
                float bossT=SegmentHit(from,to,Boss.position,1.15f),addT=RangedImmune?SegmentHit(from,to,AddPosition,.65f):float.PositiveInfinity;
                bool hit=!float.IsPositiveInfinity(Mathf.Min(bossT,addT));
                if(!shot.scope.Equals(runner.Authority.Scope)||_clock>=shot.expires||hit)
                {
                    if(hit&&shot.scope.Equals(runner.Authority.Scope))ResolveHit(addT<bossT,true,shot.scope);
                    Destroy(shot.visual.gameObject);_shots.RemoveAt(i);
                }
                else shot.visual.position=to;
            }
        }
        private static float SegmentHit(Vector3 start,Vector3 end,Vector3 center,float radius)
        {
            start.y=0;end.y=0;center.y=0;var v=end-start;var offset=start-center;
            if(offset.sqrMagnitude<=radius*radius)return 0;
            float a=Vector3.Dot(v,v);if(a<.000001f)return float.PositiveInfinity;
            float b=2*Vector3.Dot(offset,v),c=Vector3.Dot(offset,offset)-radius*radius,disc=b*b-4*a*c;
            if(disc<0)return float.PositiveInfinity;float t=(-b-Mathf.Sqrt(disc))/(2*a);
            return t>=0&&t<=1?t:float.PositiveInfinity;
        }
        private void ResolveHit(bool add,bool ranged,BossAttemptScope scope)
        {
            var where=add?AddPosition:Boss.position;int damage=add?20:ranged?15:10;
            var result=add?_combat.HitAdd(scope,damage):_combat.HitBoss(scope,damage,ranged);
            if(result==SandboxHit.RangedShield){Pop(where,"IMMUN - DREP ADD",Cyan);Burst(where,Cyan);Message="Avstandsskudd blokkeres av den bla energilinjen. Drep skjoldvokteren.";return;}
            if(result==SandboxHit.RuneShield){Pop(where,"RUNESKJOLD",Gold);Burst(where,Gold);Message="Gullskjold: aktiver tre runer med E.";return;}
            if(result!=SandboxHit.Hit&&result!=SandboxHit.Defeated)return;
            Pop(where,"-"+damage,ranged?Cyan:Color.white);Burst(where,ranged?Cyan:Gold);_flashUntil=_clock+.16f;_shakeUntil=_clock+.12f;
            Message=add?"Treff pa skjoldvokteren!":"Treff!";
            if(add&&result==SandboxHit.Defeated){Pop(where+Vector3.up,"AVSTANDSSKJOLD BRUTT",Green);Message="Add beseiret! Bossen kan na skades med avstandsangrep.";_shakeUntil=_clock+.3f;}
            else if(!add&&Shielded)Message="Fase 2: gullskjold. Aktiver de tre lysende runene!";
            if(Won){Message="Vokteren er beseiret!";_visual.Hazard.gameObject.SetActive(false);}
        }
        public void Interact()
        {
            if(!Ready||Won||Lost||_paused)return;
            if(!Shielded){Message="Runene aktiveres i fase 2, ved 70 % bossliv.";return;}
            for(int i=0;i<3;i++)if(!_runes[i]&&FlatDistance(player.position,_visual.Runes[i].position)<1.6f)
            {
                _runes[i]=true;RuneCount++;Pop(_visual.Runes[i].position+Vector3.up,"RUNE AKTIV",Green);Burst(_visual.Runes[i].position,Gold);
                Message="Rune "+RuneCount+" / 3 aktivert";
                if(RuneCount==3){runner.Authority.SetRunes(true);runner.Authority.Step();Message="Runeskjold brutt! Siste fase.";Pop(Boss.position,"FASE 3",Red);}
                RefreshVisuals();return;
            }
            Message="Ga helt bort til en lysende rune og trykk E.";
        }
        private void AdvanceAdd(float dt)
        {
            if(!RangedImmune)return;var off=player.position-AddPosition;off.y=0;
            if(off.magnitude>1.5f)_visual.Add.position+=off.normalized*1.15f*dt;
            if(off.sqrMagnitude>.01f)_visual.Add.rotation=Quaternion.LookRotation(off);
            if(off.magnitude<1.8f&&_clock>=_nextAddAttack){_nextAddAttack=_clock+1.8f;HurtPlayer(10);Burst(AddPosition,Red);}
        }
        private void AdvanceHazard()
        {
            bool p3=Phase.Equals(FakeEncounterAuthority.P3);
            if(_pulseAt<0&&_clock>=_nextPulse)
            {
                _pulseRadius=p3?4.5f:3.6f;_pulseAt=_clock+(p3?.85f:1.15f);
                _visual.Hazard.position=new Vector3(Boss.position.x,.02f,Boss.position.z);
                _visual.Hazard.localScale=new Vector3((_pulseRadius+.4f)*2,.008f,(_pulseRadius+.4f)*2);
                _visual.Hazard.gameObject.SetActive(true);Message="OMRADEANGREP - ut av den rode sirkelen!";
            }
            if(_pulseAt<0)return;
            _visual.Paint(_visual.Hazard.GetComponent<Renderer>(),Color.Lerp(new Color(.5f,.1f,.06f),Red,Mathf.PingPong(_clock*3,1)));
            if(_clock<_pulseAt)return;
            if(FlatDistance(player.position,Boss.position)<_pulseRadius+.4f)HurtPlayer(20);
            Burst(Boss.position,Red);_visual.Hazard.gameObject.SetActive(false);_pulseAt=-1;_nextPulse=_clock+(p3?1.5f:2.4f);
        }
        private void HurtPlayer(int amount)
        {
            if(_clock<_dashUntil||Lost)return;
            PlayerHealth=Mathf.Max(0,PlayerHealth-amount);_playerFlash=_clock+.25f;_shakeUntil=_clock+.2f;Pop(player.position,"-"+amount,Red);
            if(Lost){runner.Authority.Runtime.StopAttempt(new BossStopCommand(runner.Authority.Runtime.Definition.BossId,runner.Authority.Scope,"SandboxPlayerDied"));Message="Du falt. R starter et nytt forsok.";}
        }
        private void Pop(Vector3 pos,string text,Color color)
        {
            if(_feedback.Count>=20)_feedback.RemoveAt(0);_feedback.Add(new Feedback{position=pos+Vector3.up,text=text,color=color,until=_clock+1});
        }
        private void Burst(Vector3 pos,Color color)
        {
            if(_effects.Count>=16){Destroy(_effects[0].visual.gameObject);_effects.RemoveAt(0);}
            _effects.Add(new Effect{visual=_visual.Burst(pos,color),until=_clock+.35f});
        }
        private static float FlatDistance(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
        private void RefreshVisuals()
        {
            var color=_clock<_flashUntil?Color.white:Shielded?new Color(.85f,.52f,.14f):Phase.Equals(FakeEncounterAuthority.P3)?new Color(.78f,.22f,.13f):new Color(.38f,.48f,.62f);
            _visual.Refresh(_clock,!Won&&RangedImmune,!Won&&Shielded,!Won&&RangedImmune,_runes,_aimPoint,color);
            _visual.Swing.gameObject.SetActive(!Won&&!Lost&&_clock<_swingUntil);_visual.Paint(player.GetComponent<Renderer>(),_clock<_playerFlash?Color.white:Green);
            Boss.localScale=_bossScale*(_clock<_flashUntil?1.04f:1);
            arenaCamera.transform.position=_cameraHome+(_clock<_shakeUntil?new Vector3(Mathf.Sin(_clock*110),Mathf.Cos(_clock*137),0)*.055f:Vector3.zero);
        }
        public void Restart()
        {
            if(!Ready)return;
            runner.Authority.Reset(123);_combat.Reset();PlayerHealth=100;RuneCount=0;player.position=_spawn;_visual.Add.position=new Vector3(4.5f,1,0);
            _clock=0;_nextAttack=0;_nextShot=0;_nextDash=0;_dashUntil=0;_nextPulse=3;_pulseAt=-1;_nextAddAttack=2;_flashUntil=0;_shakeUntil=0;_playerFlash=0;_swingUntil=0;_paused=false;
            foreach(var shot in _shots)Destroy(shot.visual.gameObject);_shots.Clear();foreach(var e in _effects)Destroy(e.visual.gameObject);_effects.Clear();_feedback.Clear();
            for(int i=0;i<3;i++)_runes[i]=false;_visual.Hazard.gameObject.SetActive(false);_direction=Vector3.forward;_aimPoint=Boss.position;
            Message="Nytt forsok. Drep skjoldvokteren for a apne bossen for avstandsangrep.";RefreshVisuals();
        }
        private void OnGUI()
        {
            if(!Ready)return;
            if(_title==null){_title=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold};_body=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true};_small=new GUIStyle(_body){fontSize=13};_number=new GUIStyle(_title){fontSize=21,alignment=TextAnchor.MiddleCenter};_center=new GUIStyle(_body){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};}
            var matrix=GUI.matrix;float scale=Mathf.Max(.1f,Mathf.Min(Screen.width/1280f,Screen.height/720f));GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
            Panel(new Rect(18,18,300,145));GUI.Label(new Rect(32,27,276,32),"VOKTERENS ARENA",_title);
            Bar(new Rect(32,66,270,12),PlayerHealth/100f,Green);GUI.Label(new Rect(32,84,270,27),"DITT LIV "+PlayerHealth+"   |   RUNER "+RuneCount+"/3",_small);
            GUI.Label(new Rect(32,113,270,38),RangedImmune?"01  Drep skjoldvokteren":Shielded?"02  Aktiver de tre runene":"03  Beseir vokteren",_body);
            float bx=w-436;Panel(new Rect(bx,18,418,143));GUI.Label(new Rect(bx+16,27,385,32),"MUMMY GUARDIAN",_title);
            Bar(new Rect(bx+16,66,386,16),runner.Authority.HealthPercent/100f,Shielded?Gold:Red);
            GUI.Label(new Rect(bx+16,91,386,26),runner.Authority.HealthPercent+" LIV  |  "+(Shielded?"FASE 2":Phase.Equals(FakeEncounterAuthority.P3)?"FASE 3":"FASE 1"),_small);
            var saved=GUI.color;GUI.color=Shielded?Gold:RangedImmune?Cyan:Green;
            GUI.Label(new Rect(bx+16,118,386,26),Shielded?"RUNESKJOLD: ALLE TRE RUNER MA AKTIVERES":RangedImmune?"IMMUN MOT SKUDD - ADD HOLDER SKJOLDET":"SKJOLD BRUTT - SKUDD TREFFER",_small);GUI.color=saved;
            if(RangedImmune&&!Won)WorldLabel(AddPosition+Vector3.up*1.45f,"SKJOLDVOKTER  "+AddHealth+" / 40",Cyan,scale,h,true);
            if(Shielded)for(int i=0;i<3;i++)WorldLabel(_visual.Runes[i].position+Vector3.up*.9f,_runes[i]?"AKTIV":"[E]  RUNE "+(i+1),_runes[i]?Green:Gold,scale,h,false);
            foreach(var f in _feedback)WorldLabel(f.position+Vector3.up*(1-(f.until-_clock))*.9f,f.text,f.color,scale,h,false);
            if(_pulseAt>_clock&&!Won&&!Lost){GUI.color=Red;GUI.Label(new Rect(w/2-170,178,340,34),"OMRADEANGREP OM "+(_pulseAt-_clock).ToString("0.0")+" s",_number);GUI.color=saved;}
            Panel(new Rect(18,h-99,w-36,81));GUI.Label(new Rect(32,h-89,w-64,32),Message,_body);
            GUI.Label(new Rect(32,h-56,w-64,28),"WASD: flytt   |   Mus: sikt   |   Venstre / J: slag   |   Hoyre / K: skudd   |   Space: dash   |   E: rune   |   R: restart   |   Esc: pause",_small);
            if(Won||Lost||_paused)
            {
                Panel(new Rect(w/2-205,h/2-85,410,175));GUI.Label(new Rect(w/2-180,h/2-60,360,45),Won?"VOKTEREN ER BESEIRET":Lost?"DU FALT":"PAUSE",_title);
                GUI.Label(new Rect(w/2-180,h/2-8,360,36),Won?"Skjold brutt. Runer aktivert. Seier.":"R starter pa nytt.",_body);
                if(GUI.Button(new Rect(w/2-180,h/2+40,360,30),_paused?"Fortsett":"Nytt forsok")){if(_paused)_paused=false;else Restart();}
            }
            GUI.matrix=matrix;
        }
        private void WorldLabel(Vector3 position,string text,Color color,float scale,float h,bool health)
        {
            var p=arenaCamera.WorldToScreenPoint(position);if(p.z<=0)return;float x=p.x/scale,y=h-p.y/scale;
            var saved=GUI.color;GUI.color=new Color(.025f,.045f,.06f,.9f);GUI.DrawTexture(new Rect(x-125,y-3,250,health?44:28),Texture2D.whiteTexture);
            GUI.color=color;GUI.Label(new Rect(x-123,y,246,27),text,_center);GUI.color=saved;
            if(health)Bar(new Rect(x-100,y+29,200,6),AddHealth/40f,Cyan);
        }
        private static void Panel(Rect rect){var c=GUI.color;GUI.color=new Color(.025f,.045f,.06f,.95f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=c;}
        private static void Bar(Rect r,float fill,Color color){var c=GUI.color;GUI.color=new Color(.13f,.18f,.22f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=color;GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(fill),r.height),Texture2D.whiteTexture);GUI.color=c;}
        private void OnDestroy(){if(_visual!=null)_visual.Dispose();if(arenaCamera!=null)arenaCamera.transform.position=_cameraHome;}
    }
}

