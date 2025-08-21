using System;

namespace TaxChain.core;

/// <summary>
/// Represents a blockchain configuration.
/// It includes properties such as the blockchain ID, name, reward amount for mining,
/// and the difficulty level for mining blocks.
/// </summary>
public struct Blockchain
{
    /// <summary>
    /// A unique blockchain identifier
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// User-defined blockchain name, for easier manipulation
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// The amount returned to a successful miner as a reward.
    /// </summary>
    public float RewardAmount { get; set; }
    /// <summary>
    /// The amount of zeros in a hash. Made for proof-of-work
    /// </summary>
    public int Difficulty { get; set; }

    public Blockchain(Guid id, string name, float rewardAmount, int difficulty)
    {
        Id = id;
        Name = name;
        RewardAmount = rewardAmount;
        Difficulty = difficulty;
    }

    public Blockchain(string name, float rewardAmount, int difficulty)
    {
        Id = Guid.NewGuid();
        Name = name;
        RewardAmount = rewardAmount;
        Difficulty = difficulty;
    }
}