using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Functionality split between generating and storing HexSpaces, this class soley for generation and destruction
public class FlipGridGenerator : MonoBehaviour {

    Coroutine activeCoroutine;


    public FlipGrid grid;

    //My singleton which does not completely remove human error, but is it egregious? 
    public static FlipGridGenerator instance;
    void Awake() {
        if (instance != null) {
            Debug.Log("More than one instance of TileGenerator found!");
            return;
        }
        instance = this;
    }
    //
    public void Start() {
        grid = FlipGrid.instance;
    }
 //Fail safe to active generation if degeneration is currently executing
    public void StartGridGeneration(FlipGridDefinition def) {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine); //This cancels generation if for some reason both coroutines are called at the same time, such as the tutorial menu or quickly quitting 
        activeCoroutine = StartCoroutine(GenerateHexGrid(def));
    }

    //Primary function of class, create a grid of hexes and store their important data in a centralized list     
    public IEnumerator GenerateHexGrid(FlipGridDefinition def) {
        //Defining local variables used in function
        float tileHeight = def.tileSize * 1.1547f; //Width multiplied by ratio of hex width to height
        float zOffset = tileHeight * 0.75f; //Height multiplied by 3/4 -- height array ratio of hex grids
        Vector3 gridCenter = new Vector3(-(def.gridDim.x * def.tileSize) / 2 + def.tileSize, 0, -(def.gridDim.y * tileHeight) / 2 + tileHeight); //Used to offset spawnPos, centers grid to scene origin
        Vector3 spawnPos = Vector3.zero; //Defualt zeroed definition

        int[] tilePools = DistributeTilesToPools(def.spawnPercentRed, def.spawnPercentGreen, def.spawnPercentBlue, def.gridDim.x, def.gridDim.y); //Setup for procgen

        //
        //Core loop, cycles through dimensions defined in inspector (level editor class)
        for (int x = 0; x <= def.gridDim.x - 1; x++) {
            for (int z = 0; z <= def.gridDim.y - 1; z++) {
                bool skipPos = false; //Defined to reference valid spawn positions

                //Placeholder procgen of tile type -- hardly works w/ even distribution
                HexSpace.HexTile newTileType = ProcGenDistrubtion(tilePools);
                //
                //Checking and defining valid spawn positions             
                if (z % 2 == 0) { //Even rows                   
                    if (x != def.gridDim.x - 1) //Checks if the last column, skips even rows for a rounded grid shape
                        spawnPos = new Vector3(x * def.tileSize, 0, z * zOffset) + gridCenter;
                    else skipPos = true;
                }
                else { //Odd rows -- needs different x offset                           
                    spawnPos = new Vector3((x * def.tileSize) - (0.5f * def.tileSize), 0, z * zOffset) + gridCenter;
                }
                //
                //Instantiation of tile gameobject, definition of HexSpace and append to list
                if (!skipPos) {
                    GameObject newTile = GameObject.Instantiate(def.prefabs[0], spawnPos, Quaternion.identity, this.transform);
                    HexSpace newHexSpace = newTile.GetComponent<HexSpace>();

                    newHexSpace.UpdateHexSpace(newHexSpace, newTileType, def.colors[(int)newTileType], new Vector2(x, z), spawnPos, newTile);

                    grid.AddHexSpace(newHexSpace);
                    grid.UpdateHexGrid();

                    newTile.name = newTileType.ToString() + "Tile(" + x + ", " + z + ")";

                    yield return new WaitForSeconds((1 + (def.gridDim.x * def.gridDim.y) / 85) / (def.gridDim.x * def.gridDim.y)); // fun hardcoded time calculation -- dependent on grid size                
                }
                //
            }
            //
        }       
        if (def.actors.Count != 0) { //Bonus loop! If the grid definition now contains actors, take another minute to populate the generated grid randomly with the actors
            int actorIndex = 0;
            List<HexSpace> temp = new List<HexSpace>(grid.hexGridContents);
            temp.Shuffle();

            yield return new WaitForSeconds(.5f);
            foreach (Actor actor in def.actors) {
                for (int i = 0; i < def.actorCount[actorIndex]; i++) {
                    spawnPos = temp[0].position;

                    Actor newActor = Instantiate(actor, spawnPos, Quaternion.identity, this.transform);
                    newActor.currentSpace = temp[0];
                    temp[0].occupied = true;
                    grid.AddActor(newActor);

                    temp.RemoveAt(0);
                }
                actorIndex++;
            }      
        }
        yield return new WaitForSeconds(1);
        grid.GridGenerated();      
    }
//Fail safe to active degeneration if generation is currently executing
    public void StartGridDegeneration(FlipGridDefinition def)
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine); //This cancels generation if for some reason both coroutines are called at the same time, such as the tutorial menu
        activeCoroutine = StartCoroutine(DegenerateHexGrid(def)); 
    }
//New loop, cycles through list of HexSpaces, triggering their destroy animations, then purging the list
    public IEnumerator DegenerateHexGrid(FlipGridDefinition def) {
        if (grid.gridActors.Count != 0) { 
            foreach (Actor actor in grid.gridActors) {
                StartCoroutine(actor.DespawnActor());
            }
            yield return new WaitForSeconds(1);
        }
        foreach(HexSpace space in grid.hexGridContents) {

            StartCoroutine(space.GetComponent<TileFlip>().DestroyTile());
            //grid.hexGridContents.Remove(space);
            yield return new WaitForSeconds((1 + (def.gridDim.x * def.gridDim.y) / 85) / (def.gridDim.x * def.gridDim.y));
        }
        grid.hexGridContents.Clear();
        yield return new WaitForSeconds((def.gridDim.x * def.gridDim.y) / 85);
        grid.GridDegenerated();
    }


//Utility function, selects tiles randomly between defined pools
//Does not work... will want to revise 
    HexSpace.HexTile ProcGenDistrubtion(int[] pools) {
        int tileIndex = -1;
        bool[] remaining = new bool[] { true, true };

        if (Random.Range(0, 1) == 0 && pools[3] > 0) {
            tileIndex = Random.Range(0, 3);
            pools[3]--;
        }
        else {
            int range = 0;
            if (pools[0] > 0) range++; else remaining[0] = false;
            if (pools[1] > 0) range++; else remaining[1] = false;
            if (pools[2] > 0) range++;
            tileIndex = Random.Range(0, range);
            if (!remaining[0]) tileIndex += 1;
            if (!remaining[1] && tileIndex != 0) tileIndex += 1;
            pools[tileIndex]--;
        }

        switch (tileIndex) {
            default:
                return HexSpace.HexTile.Blue;
            case 0:
                return HexSpace.HexTile.Red;
            case 1:
                return HexSpace.HexTile.Green;
            case 2:
                return HexSpace.HexTile.Blue;
        }
    }
//Setup for procgen, distributes total tiles to percentages of each color and random
//This function breaks if the inspector fields are filled greater than 100% distribution, is there a way to restrict the float variables so they don't exceed 1 when combined?
    static int[] DistributeTilesToPools(float redP, float greenP, float BlueP, float gridWidth, float gridHeight) {
        int totalTiles = (int)((gridWidth * gridHeight) - gridHeight / 2);
        int[] pools = new int[4];

        pools[0] = (int)(totalTiles * redP);
        pools[1] = (int)(totalTiles * greenP);
        pools[2] = (int)(totalTiles * BlueP);
        //pools[3] = totalTiles - pools[0] - pools[1] - pools[2];
        //Debug.Log(totalTiles + " TotalTiles, " + pools[0] + " RedTiles, " + pools[1] + " GreenTiles, " + pools[2] + " BlueTiles, " + pools[3] + " RandomTiles");
        return pools;
    }
//    
}

public static class IListExtensions {
    /// <summary>
    /// Shuffles the element order of the specified list.
    /// </summary>
    public static void Shuffle<T>(this IList<T> ts) {
        var count = ts.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i) {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = ts[i];
            ts[i] = ts[r];
            ts[r] = tmp;
        }
    }
}