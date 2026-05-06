using System;

public enum AnimalDiet
{
    Herbivore,
    Carnivore,
    Omnivore
}

public enum MatingSystem
{
    MonogamousPair,       // breedable females are limited by available partners
    OneMaleMultiFemale,   // one male can breed with all breedable females
    Polygamous            // multiple males/females, output still female-limited in current model
}

public enum AnimalActionType
{
    Idle,
    Eat,
    Drink,
    Move,
    Flee,
    Wander,
    Breed,
    Migrate,
    AttackPlayer,
    AttackAnimal,
    DefendAnimal,
    AttackPlayerTile,
}

[Serializable]
public struct TileCoord : IEquatable<TileCoord>
{
    public int x;
    public int y;

    public TileCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool Equals(TileCoord other) => x == other.x && y == other.y;
    public override bool Equals(object obj) => obj is TileCoord other && Equals(other);
    public override int GetHashCode() => (x * 397) ^ y;

    public static bool operator ==(TileCoord a, TileCoord b) => a.Equals(b);
    public static bool operator !=(TileCoord a, TileCoord b) => !a.Equals(b);
}
