using System;

namespace TaxChain.core;

/// <summary>
/// Represents a blockchain configuration.
/// It includes properties such as the blockchain ID, name, reward amount for mining,
/// and the difficulty level for mining blocks.
/// </summary>
public struct Blockchain
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public float RewardAmount { get; set; }
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