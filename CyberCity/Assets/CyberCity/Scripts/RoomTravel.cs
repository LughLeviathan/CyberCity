using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberCity
{
    public sealed class RoomTravel : MonoBehaviour
    {
        public InteriorRoom Room { get; private set; }
        public Citizen Occupant { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool IsInside => Room != null;
        public bool NearExit => IsInside && Vector2.Distance(game.player.transform.position, Room.exit) < 1.35f;
        public Vector2 SavePosition => IsInside || IsTransitioning ? returnPosition : (Vector2)game.player.transform.position;
        CyberCityGame game;
        GameObject world;
        Transform citizenParent;
        Vector3 citizenPosition;
        Vector2 returnPosition;
        Scene cityScene, roomScene;

        public void Initialize(CyberCityGame owner)
        {
            game = owner; cityScene = game.gameObject.scene;
            world = game.areas[0].transform.parent.gameObject;
            foreach (var npc in game.citizens) npc.gameObject.SetActive(false);
        }

        public bool CanEnter(Citizen npc)
        {
            if (npc == null || !game.Started || IsInside || IsTransitioning || game.Progress.completed < npc.unlockAfter) return false;
            var area = game.areas.FirstOrDefault(a => a.areaName == npc.area);
            return area != null && area.IsOpen && Vector2.Distance(game.player.transform.position, area.door.position) <= 1.8f;
        }

        public void Enter(Citizen npc)
        {
            if (!CanEnter(npc)) return;
            if (!Application.CanStreamedLevelBeLoaded(InteriorRoom.ScenePath(npc.citizenName)))
            { game.UI.Toast("Room scene is missing. Run Tools / CyberCity / Build NPC interiors."); return; }
            game.UI.CloseModal(); returnPosition = game.player.transform.position;
            IsTransitioning = true; StopPlayer(); StartCoroutine(EnterRoom(npc));
        }

        IEnumerator EnterRoom(Citizen npc)
        {
            yield return game.UI.FadeRoom(true, npc.citizenName);
            var load = SceneManager.LoadSceneAsync(InteriorRoom.ScenePath(npc.citizenName), LoadSceneMode.Additive);
            yield return load;
            roomScene = SceneManager.GetSceneByPath(InteriorRoom.ScenePath(npc.citizenName));
            Room = roomScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InteriorRoom>()).Single();
            world.SetActive(false);
            citizenParent = npc.transform.parent; citizenPosition = npc.transform.position;
            npc.transform.SetParent(null); SceneManager.MoveGameObjectToScene(npc.gameObject, roomScene);
            Occupant = npc; npc.transform.position = Room.mentor; npc.gameObject.SetActive(true);
            Teleport(Room.spawn);
            yield return game.UI.FadeRoom(false, Room.title);
            IsTransitioning = false;
            game.UI.Toast(Room.title + "  /  E: " + npc.citizenName + "  /  EXIT: pintu selatan");
        }

        public void Exit(Action after = null)
        {
            if (!IsInside || IsTransitioning) return;
            game.UI.CloseModal(); IsTransitioning = true; StopPlayer(); StartCoroutine(ExitRoom(after));
        }

        IEnumerator ExitRoom(Action after)
        {
            yield return game.UI.FadeRoom(true, "CYBERCITY");
            Occupant.gameObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(Occupant.gameObject, cityScene);
            Occupant.transform.SetParent(citizenParent); Occupant.transform.position = citizenPosition;
            // The two scenes share coordinates; retire room colliders before restoring the city.
            Room.gameObject.SetActive(false);
            Occupant = null; Room = null;
            world.SetActive(true); Teleport(returnPosition);
            yield return SceneManager.UnloadSceneAsync(roomScene);
            yield return game.UI.FadeRoom(false, "CYBERCITY");
            IsTransitioning = false; game.Save(); after?.Invoke();
        }

        void StopPlayer()
        {
            game.player.CanMove = false;
            game.player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }

        void Teleport(Vector2 position)
        {
            StopPlayer(); game.player.GetComponent<Rigidbody2D>().position = position;
            game.player.transform.position = position; Physics2D.SyncTransforms();
        }
    }
}
