# Taxchain

A blockchain implementation for the purposes of tax administration. Note this project is mostly educational and has various security vulnarabilities, hence it should not be used for actual administration.

## Description

Users leverage the CLI client to communicate with a daemon. This daemon, running in the background, is responsible for managing a local blockchain repository and synchronization of local blockchains with a blockchain network.

The daemon does not have to be running continuously. All the blockchain information is stored locally in a durable storage (e.g. PostgreSQL), which is accessed anytime the daemon boots up. If no such storage exists, the daemon sets up a new one. Furthermore, the daemon automatically synchronizes its blockchain information with the network (the interval for synchronization can be configured) and updates this local storage.

## Installation

Run `git clone` on this repository.  

## Requirements

The current version is only supported on Linux.

- .NET SDK version 8 or higher
    - check [.NET docs](https://learn.microsoft.com/en-us/dotnet/core/sdk) for more info
- PostgreSQL database
    - make sure you have `postgresql` and `postgresql-contrib` installed. If not, run `sudo apt install postgresql postgresql-contrib` if you have the APT package-manager.
    - you can check you have it installed by running `psql --version`
    - once you have it installed, we need to set up authentication for the database, see [App setup](#app-setup) below.

## App setup
The application is set up using a `.env` file. Navigate to the project's base directory, go to `./src/TaxChain.Daemon` and create a new `.env` file here. Let's set it up to contain this information:
```.env
TAXCHAIN_DB_HOST=postgres
TAXCHAIN_DB_PORT=5432  #Default port for PostgreSQL
TAXCHAIN_DB_USER=postgres
TAXCHAIN_DB_PASSWORD=postgres
TAXCHAIN_DB_NAME=taxchains
TAXCHAIN_ADMIN_DB=Host=localhost;Username=postgres;Password=postgres;Database=postgres
RECEIVER_PORT=4662  #The port for listening for incoming requests
DISCOVERY_INTERVAL=30  #The time interval of peer discovery in our network
```
We mostly have to configure our database connection. Make sure you're correctly setting up TAXCHAIN_ADMIN_DB: the administrator's credentials for accessing PostgreSQL - the app uses it for creating a new database (defined by TAXCHAIN_DB_USER, TAXCHAIN_DB_PASSWORD, and TAXCHAIN_DB_NAME) if it does not exist yet. If unsure about the setup, use the values displayed above, only changing TAXCHAIN_ADMIN_DB to match your own credentials.

> **Warning**:
You may encounter an issue with peer authentication. By default, postgresql uses peer authentication for the postgres superuser. We will have to switch to `md5` authentication.
> First, make sure you know your postgres password. If you don't, update it using 
```
sudo -u postgres psql
ALTER USER postgres PASSWORD 'postgres';
\q
```
>You will use this password in `.env`.
Please, find your hba_file (e.g. by running `sudo -u postgres psql -c 'SHOW hba_file';`) and make sure that 
```
local    all       postgres                   peer
```
> is changed to
```
local    all       postgres                   md5
```
> Run `sudo systemctl restart postgresql`. Now we have our DB setup ready.


## Usage

The base command is
```cs
dotnet run --project ./src/TaxChain.CLI/TaxChain.csproj *CLI arguments*
```

### CLI arguments

- `-h|--help` or without arguments/options
    - Run this command whenever unsure about the usage or commands.

#### Daemon commands

- `start`
    - Starts up the daemon. This command is not necessary as [blockchain management commands](#blockchain-management) and [individual-blockchain commands](#individual-blockchain-commands) check the daemon's status and boot it up if needed.

- `kill`
    - Gracefully stops the daemon. 

- `status`
    - Retrives the status of daemon - whether it is running, for how long, whether it is currently mining, etc.

#### Blockchain management
The base command for this type of commands is 
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.csproj blockchain <BLOCKCHAIN_ID>
``` 
All arguments in this section can be postfixed with the `--verbose` option to display more logging.

- `sync`
    - Synchronizes all locally stored blockchains against the network. The conflict resolution is straightforward (and not that safe - again, this project is not meant for real administation): the longest valid blockchain is the correct one. The definition of a valid blockchain is described lower under the `verify` command.

- `fetch -c|-chain <CHAIN_ID>`
    - Fetches the particular blockchain from the network. If it does not exist in the local storage, it gets stored. If it exists locally, the local copy is compared against the ones coming from the network. For resolution, check the `sync` command.

- `create`
    - Creates a new blockchain and sets it up locally (together with a genesis block). When run, the client asks for futher description:
        - *name*
        - *reward* - The rewards each miner gets after completing the mining process.
        - *difficulty* - The amount of zero that block's hash has to have to get appended to the blockchain. This achieves a so called [proof of work](https://en.wikipedia.org/wiki/Proof_of_work).

- `list`
    - Lists all locally stored blockchains (their names, rewards, and difficulties).

- `connect --host <HOST IP OR ADDRESS> --port <PORT>`
    - Connects to the specified address and adds this peer to our network.

#### Individual blockchain commands

The base command for this type of commands is `dotnet run --project ./src/TaxChain.CLI/TaxChain.csproj <BLOCKCHAIN_ID>`. If unsure about the particular blockchain's ID, run the `list` command mentioned above.

- `add`
    - Adds a new transaction to pending transactions. When run, the CLI asks for futher details: taxpayer ID, amount,... Should you wish for the new transaction to get truly appended to the blockchain, run the `mine` command. 

- `gather -u <TaxpayerID>`
    - Gathers all the valid transactions within the blockchain belonging to the taxpayer and displays them.

- `ledger`
    - Lists 5 last blocks added to the blockchain. The number of blocks can be changed with additional option `-n|--number <AMOUNT>`.

- `remove`
    - Removes the blockchain from the local storage, together with all blocks and transactions related.

- `mine`
    - Starts mining the oldest pending transaction. The mined transaction gets appended to the blockchain and is considered valid. If a transaction gets 'added' through `add` but no `mine` command is run, the transaction is never added to the blockchain.
    - **Only one** transaction can be mined at a time. Check the [status command](#daemon-commands) to verify whether the daemon is currently mining.

- `info`
    - Retrieves details about the blockchain and displays it.

- `verify`
    - Attempts to validate the blockchain by going through all its blocks. For a block to be valid, its hash has to be recomputed in place and match the stored value, its hash has to start with a certain amount of zeros (see [difficulty]() above), and its previous hash has to match the hash of its nearest previous block.
    A blockchain is valid when all its blocks are valid.


### Example usage

Let's go through creating our own blockchain, adding a block and mining it.

*Let's create a new blockchain*
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj create

What's the name of the new taxchain? BrandNew
What's the reward amount for mining? 2 
What's the difficulty for proof-of-work (1-5 range recommended)? 3
Sending creation request to the daemon...
Checking daemon status...
Connecting to the daemon. Might take up to 5 seconds...
Daemon not running, starting now...
/home/jakub/Documents/uk/csharp/tax-registry-blockchain/src/TaxChain.Daemon/P2P/P2PNode.cs(39,23): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/jakub/Documents/uk/csharp/tax-registry-blockchain/src/TaxChain.Daemon/TaxChain.Daemon.csproj]
Daemon program runs
info: TaxChain.Daemon.Services.ControlService[0]
      Control service started on pid 82866
info: TaxChain.Daemon.Services.ControlService[0]
      Initializing storage...
info: TaxChain.Daemon.Services.ControlService[0]
      Control service listening for commands...
info: TaxChain.Daemon.Services.ControlService[0]
      Waiting for client connection...
info: TaxChain.Daemon.Services.ControlService[0]
      Client connected
info: TaxChain.Daemon.Services.ControlService[0]
      Received command
info: TaxChain.Daemon.Services.ControlService[0]
      Processing command: status
info: TaxChain.Daemon.Services.ControlService[0]
      Command processed successfully
Daemon started successfully
info: TaxChain.Daemon.Services.ControlService[0]
      Client connection handled
Daemon is running now.
Connecting to the daemon. Might take up to 5 seconds...
Taxchain creation successful!. Here's its id: 595fe7fc-456f-4425-b585-49e7b5bac9ce
```

*let's check it has been created*
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj list
Checking daemon status...
Connecting to the daemon. Might take up to 5 seconds...
Sending a list request to the chain deamon.
Sending a list request to the chain deamon.
Connecting to the daemon. Might take up to 5 seconds...
┌──────────────────────────────────────┬──────────┬──────────────┬────────────┐
│ chainId                              │ name     │ rewardAmount │ difficulty │
├──────────────────────────────────────┼──────────┼──────────────┼────────────┤
│ 9996f34e-fb37-4087-9589-a17311d104f1 │ Bon      │ 2            │ 5          │
│ 595fe7fc-456f-4425-b585-49e7b5bac9ce │ BrandNew │ 2            │ 3          │
```

*It is there! Now add a new block*
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj blockchain 595fe7fc-456f-4425-b585-49e7b5bac9ce add

Type your taxpayer id: JohnSteward
Write the amount: 30
Checking daemon status...
Connecting to the daemon. Might take up to 5 seconds...
Sending provided transaction to the records...
Connecting to the daemon. Might take up to 5 seconds...
Successfully added a block to chain 595fe7fc-456f-4425-b585-49e7b5bac9ce!
If you want to ensure it gets appended, run the 'mine' command.
```

*And mine!*
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj blockchain 595fe7fc-456f-4425-b585-49e7b5bac9ce mine
Checking daemon status...
Connecting to the daemon. Might take up to 5 seconds...
Calling the daemon to start mining...
Connecting to the daemon. Might take up to 5 seconds...
Starting mining, looking for 000 prefix
Found a valid nonce! 91
Mining of blockchain 595fe7fc-456f-4425-b585-49e7b5bac9ce has started!
``` 

If you look carefully, you can see the mining process has finished before the client managed to respond - 'Found a valid nonce' message appears once the mining finishes. Let's check the daemon is truly not mining.

```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj status
Connecting to the daemon. Might take up to 5 seconds...
Daemon is running.
Daemon's status:
Status: Running
Process id: 82866
Uptime: 00:06:03.2081608
TimeStamp: 8/18/2025 10:19:34 AM
Mining: False              <----- NOT MINING
Last sync success: True
Last sync timestamp: 8/18/2025 10:19:31 AM
```

If we try to mine again, nothing happens as no pending transactions are waiting.
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj blockchain 595fe7fc-456f-4425-b585-49e7b5bac9ce mine
Checking daemon status...
Connecting to the daemon. Might take up to 5 seconds...
Calling the daemon to start mining...
Connecting to the daemon. Might take up to 5 seconds...
Nothing to mine...
```

We can check everything has gone smoothly by running `ledger` or `gather` on our taxpayer:
```
dotnet run --project ./src/TaxChain.CLI/TaxChain.CLI.csproj blockchain 595fe7fc-456f-4425-b585-49e7b5bac9ce gather -u JohnSteward
Checking daemon status...
Connecting to the daemon. Might take up to 5 seconds...
Sending a request for the gather command...
Connecting to the daemon. Might take up to 5 seconds...
ID: JohnSteward
Amount: 30.00000000
```
