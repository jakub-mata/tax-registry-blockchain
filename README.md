# Taxchain

A blockchain implementation for the purposes of tax administration. Note this project is mostly educational and has various security vulnarabilities, hence it should not be used for actual administration.

## Description

Users leverage the CLI client to communicate with a daemon. This daemon, running in the background, is responsible for managing a local blockchain repository and synchronization of local blockchains with a blockchain network.

The daemon does not have to be running continuously. All the blockchain information is stored locally in a durable storage (e.g. PostgreSQL), which is accessed anytime the daemon boots up. If no such storage exists, the daemon sets up a new one. Furthermore, the daemon automatically synchronizes its blockchain information with the network (the interval for synchronization can be configured) and updates this local storage.

## Installation

Run `git clone` on this repository. Inside the repository, run dotnet    

## Requirements

### App setup
The application is set up using a `.env` file. Navigate to the project's base directory. We have to create an env file with authentication information:
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

- .NET SDK version 8 or higher
    - check [.NET docs](https://learn.microsoft.com/en-us/dotnet/core/sdk) for more info
- PostgreSQL database
    - Linux: make sure you have `postgresql` and `postgres-contrib` installed. If not, run `sudo apt install postgresql postgres-contrib` if you have the APT package-manager.
    - you can check you have it installed by running `psql --version`
    - Windows
    - once you have it installed, we need to set up authentication for the database, see [Installation](#installation) above.
-

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

- `stop`
    - Gracefully stops the daemon. 

- `status`
    - Retrives the status of daemon - whether it is running, for how long, whether it is currently mining, etc.

#### Blockchain management
The base command for this type of commands is `dotnet run --project ./src/TaxChain.CLI/TaxChain.csproj <BLOCKCHAIN_ID>`. All arguments in this section can be postfixed with the `--verbose` option to display more logging.

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
