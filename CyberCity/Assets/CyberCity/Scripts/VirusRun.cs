using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberCity
{
    // Simulation units are the arcade viewport's UI units, independent of display resolution.
    public sealed class VirusRun
    {
        public const int TotalViruses = 24;
        public const float Width = 752, Height = 428;
        public sealed class Virus { public int id; public Vector2 position; }
        public sealed class Shot { public int id; public Vector2 position, velocity; }
        readonly List<Virus> viruses = new List<Virus>();
        readonly List<Shot> shots = new List<Shot>();
        readonly System.Random random = new System.Random(37);
        public IReadOnlyList<Virus> Viruses => viruses;
        public IReadOnlyList<Shot> Shots => shots;
        public Vector2 Player { get; private set; } = new Vector2(Width/2,Height/2);
        public int Health { get; private set; } = 3;
        public int Kills { get; private set; }
        public int Wave { get; private set; } = 1;
        public bool Paused { get; set; }
        public bool Won => Kills == TotalViruses && Health > 0;
        public bool Finished => Won || Health == 0;
        public bool Invulnerable => hurtCooldown > 0;
        public float Elapsed { get; private set; }
        int spawned, nextId;
        float fireCooldown, spawnCooldown = .7f, hurtCooldown;

        public Vector2 AutoAim()
        {
            Vector2 target = Vector2.up; float distance = float.MaxValue;
            foreach (var virus in viruses)
            {
                Vector2 delta = virus.position - Player;
                if (delta.sqrMagnitude < distance) { target = delta; distance = delta.sqrMagnitude; }
            }
            return target.normalized;
        }

        public void Step(float deltaTime, Vector2 movement, Vector2 aim, bool fire)
        {
            if (Paused || Finished || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            float dt = Mathf.Min(deltaTime,.05f); Elapsed += dt;
            fireCooldown -= dt; spawnCooldown -= dt; hurtCooldown -= dt;
            Player += Vector2.ClampMagnitude(movement,1) * (220*dt);
            Player = new Vector2(Mathf.Clamp(Player.x,20,Width-20),Mathf.Clamp(Player.y,20,Height-20));
            if (fire && fireCooldown <= 0 && aim.sqrMagnitude > .001f)
            {
                shots.Add(new Shot { id = ++nextId, position = Player, velocity = aim.normalized * 540 });
                fireCooldown = .16f;
            }
            if (spawnCooldown <= 0 && spawned < Wave*8 && viruses.Count < 5)
            {
                Vector2 p;
                switch (random.Next(4))
                {
                    case 0: p = new Vector2(12,(float)random.NextDouble()*Height); break;
                    case 1: p = new Vector2(Width-12,(float)random.NextDouble()*Height); break;
                    case 2: p = new Vector2((float)random.NextDouble()*Width,12); break;
                    default: p = new Vector2((float)random.NextDouble()*Width,Height-12); break;
                }
                if (Vector2.Distance(p,Player) < 150) p = new Vector2(Width-p.x,Height-p.y);
                viruses.Add(new Virus { id = ++nextId, position = p }); spawned++; spawnCooldown = .65f;
            }
            for (int i = shots.Count-1; i >= 0; i--)
            {
                var shot = shots[i]; Vector2 before = shot.position; shot.position += shot.velocity*dt;
                bool hit = false;
                for (int v = viruses.Count-1; v >= 0; v--)
                {
                    if (SegmentDistance(before,shot.position,viruses[v].position) > 18) continue;
                    viruses.RemoveAt(v); Kills++; hit = true; break;
                }
                if (hit || shot.position.x < 0 || shot.position.x > Width || shot.position.y < 0 || shot.position.y > Height) shots.RemoveAt(i);
            }
            foreach (var virus in viruses)
            {
                virus.position = Vector2.MoveTowards(virus.position,Player,(48+Wave*13)*dt);
                if (Vector2.Distance(virus.position,Player) < 27 && hurtCooldown <= 0)
                {
                    Health--; hurtCooldown = 1.2f;
                    Vector2 push = (virus.position-Player).normalized;
                    virus.position += (push.sqrMagnitude > 0 ? push : Vector2.up)*65;
                    if (Health == 0) break;
                }
            }
            if (!Finished && Kills == Wave*8 && Wave < 3) { Wave++; spawnCooldown = 1.1f; shots.Clear(); }
        }

        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 point)
        {
            Vector2 segment = b-a;
            float t = segment.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(point-a,segment)/segment.sqrMagnitude);
            return Vector2.Distance(point,a+segment*t);
        }
    }
}
