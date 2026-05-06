// public interface IEvaluator<T>
// {
//     /// <summary>
//     /// Evaluates an instance of T and returns a priority score.
//     /// </summary>
//     /// <param name="item">The item to evaluate.</param>
//     /// <returns>An integer score (higher is better).</returns>
//     int Evaluate(T item);
// }

// /// <summary>
// /// A non-generic evaluator for situations where no specific input is required (e.g., global evaluations).
// /// </summary>
// public interface IEvaluator
// {
//     /// <summary>
//     /// Evaluates without an external context and returns a priority score (higher is better).
//     /// </summary>
//     int Evaluate();
// }