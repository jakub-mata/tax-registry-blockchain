using System;

namespace TaxChain.core;

public struct Blockchain
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int RewardAmount { get; set; }
    public int Difficulty { get; set; }

    public Blockchain(Guid id, string name, int rewardAmount, int difficulty)
    {
        Id = id;
        Name = name;
        RewardAmount = rewardAmount;
        Difficulty = difficulty;
    }

    public Blockchain(string name, int rewardAmount, int difficulty)
    {
        Name = name;
        RewardAmount = rewardAmount;
        Difficulty = difficulty;
    }
}