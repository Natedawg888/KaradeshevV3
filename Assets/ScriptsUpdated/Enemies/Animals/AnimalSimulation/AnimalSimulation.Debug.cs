using UnityEngine;

public partial class AnimalSimulation
{
    private const bool ANIMAL_DEBUG = false;

    private void LogAnimalEvent(string tag, AnimalGroupState group, string message)
    {
        if (!ANIMAL_DEBUG)
            return;

        string speciesName = group.species != null ? group.species.name : "NULL";

        //Debug.Log(
            //$"[ANIMAL {tag}] " +
            //$"id={group.id} species={speciesName} size={group.size} " +
            //$"hp={group.currentHealth}/{group.MaxHealth} " +
            //$"hunger={group.hunger:F2} thirst={group.thirst:F2} " +
            //$"tile={group.tile} action={group.lastAction} :: {message}");
    }

    private void LogAnimalVsAnimal(string tag, AnimalGroupState actor, AnimalGroupState other, string message)
    {
        if (!ANIMAL_DEBUG)
            return;

        string actorName = actor.species != null ? actor.species.name : "NULL";
        string otherName = other.species != null ? other.species.name : "NULL";

        //Debug.Log(
            //$"[ANIMAL {tag}] " +
            //$"actor[id={actor.id}, species={actorName}, size={actor.size}, hp={actor.currentHealth}/{actor.MaxHealth}, tile={actor.tile}] " +
            //$"other[id={other.id}, species={otherName}, size={other.size}, hp={other.currentHealth}/{other.MaxHealth}, tile={other.tile}] :: {message}");
    }
}
