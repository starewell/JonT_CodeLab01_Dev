using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FlipGridGenerator))]
public class FlipGrid : MonoBehaviour {

    /// <summary>
    /// This class is used for the managing of the HexGrid after it's been generated by the GridGenerator class. They directly interface
    /// The FlipGrid stores each HexSpace while it is being generated
    /// </summary>

    FlipGridGenerator generator;
    [HideInInspector]
    public FlipGridDefinition gridDef; //We'll find a more clever way to pass this in the future

//Whole lotta events for when anything about the grid changes... Takes rgb arguments for a minority of functions, at this point I should refactor into more events
//How free are events? Like should I not have so many....
    public delegate void OnGridChange(float r, float g, float b);
    public event OnGridChange GridCalculatedCallback;
    public event OnGridChange GridGeneratedCallback;
    public event OnGridChange TotalsChangeCallback;
    public event OnGridChange SceneEndCallback;

    public delegate void OnPlayerAction(LevelStateMachine.LevelState state);
    public event OnPlayerAction LevelExitCallback;
    public event OnPlayerAction TurnTakenCallback;

    public delegate void OnGridCondition(string[] lvl);
    public event OnGridCondition GridSolvedCallback;
//
    public List<HexSpace> hexGridContents = new List<HexSpace>();  //Stored list of all tiles in grid; HexSpaces
    public List<Actor> gridActors = new List<Actor>(); //Stored list of all the actors on the grid; Actors

    bool won;

//My singleton which does not completely remove human error, but is it egregious? 
    public static FlipGrid instance;
    void Awake() {
        if (instance != null) {
            Debug.Log("More than one instance of FlipGrid found!");
            return;
        }
        instance = this;
    }
//
    private void Start() {
        generator = GetComponent<FlipGridGenerator>();
    }
//Trigger generator, invoke TotalsDisplay to update
    public void GenerateGrid() {
        GridCalculatedCallback?.Invoke(gridDef.goalPercentRed, gridDef.goalPercentGreen, gridDef.goalPercentBlue);
        generator.StartGridGeneration(gridDef);

    }
//
//Unlock grid and bark if grid is a level
    public void GridGenerated() { 
        if(gridDef.level) {
            GridGeneratedCallback?.Invoke(0, 0, 0);
        }
        UnlockHexGrid();
    }
//
//Trigger generator, lock grid
    public void DegenerateGrid(bool win) {
        LockHexGrid();
        won = win;
        generator.StartGridDegeneration(gridDef);
        LevelExitCallback?.Invoke(LevelStateMachine.LevelState.Cleanup);

        if (win) {
            string[] lvls = new string[gridDef.unlocks.Length];
            for (int i = 0; i < gridDef.unlocks.Length; i++) {
                lvls[i] = gridDef.unlocks[i];
            }
            GridSolvedCallback?.Invoke(lvls);
        }
    }
//
//Trigger FlipGameManager from generator call
    public void GridDegenerated() { 
        if(gridDef.level) {
            SceneEndCallback?.Invoke(0, 0, 0);
        }
    }
//
//Append new HexSpace to list, subscribe for updates
    public void AddHexSpace(HexSpace space) {
        hexGridContents.Add(space);

        space.GetComponent<TileFlip>().FlipCallback += FlipHexSpace;
        space.GetComponent<TileFlip>().PlayerActionCallback += PlayerActionTaken;
        space.GetComponent<TileFlip>().OriginCallback += UpdateAdjacent;
    }
//
//Append new Actor to list... that's it for now
    public void AddActor(Actor actor) {
        gridActors.Add(actor);


    }

//Subscribed function, called from Tile Flip class so the Grid knows when it's been altered by the player versus an Actor
    public void PlayerActionTaken(HexSpace space, bool playerInput) { //Useless paramaters here, passing to access the event
        if (gridActors.Count != 0) {
            LockHexGrid();
            foreach (Actor actor in gridActors)
                actor.takingTurn = true;
            StartCoroutine(WaitForActors());
        }
    }
//The grid has the reference to all Actors, so when they are done taking their turn we know it's the players turn again.
//Currently, since the Actor's takingTurn bool isn't working properly, this isn't doing much
//But the Actors move so quickly it percievably works
    public IEnumerator WaitForActors() { 
        foreach (Actor actor in gridActors) { 
            while(actor.takingTurn) {
                yield return null;
            }
        }
        TurnTakenCallback?.Invoke(LevelStateMachine.LevelState.PlayerTurn);
    }

//Updates HexSpace to flipped values when called from TileFlip.Interact() or from being flipped adjacently
    void FlipHexSpace(HexSpace space, bool playerInput) {
    	int index = (int)space.hexTile + 1;
    	if (index > System.Enum.GetValues(typeof(HexSpace.HexTile)).Length - 1) index = 0;

    	space.hexObject.GetComponent<Renderer>().material = gridDef.colors[index];
    	space.hexTile = (HexSpace.HexTile)index;
        space.hexObject.name = space.hexTile.ToString() + "Tile(" + space.coordinate.x + ", " + space.coordinate.y + ")";

        UpdateHexGrid();
    }
//Function that parses if a coord holds a HexSpace, and if it's not occupied, it then flips them on a juice delay
    IEnumerator FlipAdjacentHexes(Vector2[] adjacentCoord, bool playerInput) { 
        foreach (Vector2 coord in adjacentCoord) {
            if (hexGridContents.Find(space => space.coordinate == coord) != null)
                if (!hexGridContents.Find(space => space.coordinate == coord).occupied) { //Found this constructor thru microsoft docs, have never used it before
                    HexSpace adjSpace = hexGridContents.Find(space => space.coordinate == coord);
                    StartCoroutine(adjSpace.GetComponent<TileFlip>().FlipTile(false)); //Trigger TileFlip class FlipTile() function w/ false origin bool
                    yield return new WaitForSeconds(.1f);
                }
        }
        if (playerInput && !won && gridActors.Count != 0) { //If these spaces were flipped by a player, switch level states to Actor turn -- can be modified for more player actions
            yield return new WaitForSeconds(.5f);
            TurnTakenCallback?.Invoke(LevelStateMachine.LevelState.ActorTurn);
        }
    }

//
//Subscribed function, calculates adjacent hex spaces and exectues Flips them
    void UpdateAdjacent(HexSpace space, bool playerInput) {

        Vector2 originCoord = space.coordinate;
        Vector2[] adjacentCoord;
        if (originCoord.y % 2 == 0) { //Even rows
            adjacentCoord = new Vector2[] {
            new Vector2(originCoord.x, originCoord.y-1),
            new Vector2(originCoord.x-1, originCoord.y),
            new Vector2(originCoord.x, originCoord.y+1),
            new Vector2(originCoord.x+1, originCoord.y-1),
            new Vector2(originCoord.x+1, originCoord.y+1),
            new Vector2(originCoord.x+1, originCoord.y)};
        }
        else { //Odd rows
            adjacentCoord = new Vector2[] {
            new Vector2(originCoord.x-1, originCoord.y-1),
            new Vector2(originCoord.x-1, originCoord.y),
            new Vector2(originCoord.x-1, originCoord.y+1),
            new Vector2(originCoord.x, originCoord.y-1),
            new Vector2(originCoord.x, originCoord.y+1),
            new Vector2(originCoord.x+1, originCoord.y)};
        }
        StartCoroutine(FlipAdjacentHexes(adjacentCoord, playerInput));
    }
//
//Useless middleman, maybe one day!
    public void UpdateHexGrid() {


        UpdateHexGridTotals();
    }
                   
//Temporarily store grid totals, call for UI to refresh after tiles flip
    void UpdateHexGridTotals() {
        int amntRed = 0, amntGreen = 0, amntBlue = 0;
        foreach (HexSpace space in hexGridContents) {
            if (space.hexTile == HexSpace.HexTile.Red) amntRed++;
            else if (space.hexTile == HexSpace.HexTile.Green) amntGreen++;
            else if (space.hexTile == HexSpace.HexTile.Blue) amntBlue++;
        }
        TotalsChangeCallback?.Invoke(amntRed, amntGreen, amntBlue);
    }
//
//Changes base class interactable's active bool 
    public void UnlockHexGrid() {
        foreach(HexSpace space in hexGridContents) {
            if (!space.occupied) //oops stay locked friend
                space.GetComponent<TileFlip>().active = true;               
        }
    }
    public void LockHexGrid() {
        foreach (HexSpace space in hexGridContents) {
            space.GetComponent<TileFlip>().active = false;
        }           
    }
//
}

