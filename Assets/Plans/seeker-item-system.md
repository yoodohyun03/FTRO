# Project Overview
- Game Title: FTRO (Hide and Seek style game)
- High-Level Concept: Survivors try to hide or complete objectives while a Seeker tries to find and eliminate them. AI Dummies roam the map to distract the seeker.
- Players: Multiplayer (Photon PUN2)
- Target Platform: PC (Windows)
- Render Pipeline: Built-in (based on the project settings provided earlier)

# Game Mechanics
## Core Gameplay Loop
- Survivors move and act like AI to hide.
- Seeker uses abilities or items to identify and catch survivors.
- AI Dummies provide visual noise.

## Proposed Item: Seeker's Tactical Device (술래 아이템)
- This item is exclusive to the Seeker.
- It has two selectable modes (or two different items):
    1. **Freeze Mode**: All AI Dummies stop moving for 5 seconds. This makes any moving character stand out as a potential survivor.
    2. **Swarm Mode**: All AI Dummies run towards the nearest survivor (or a random one) for 5 seconds. This creates chaos and forces survivors to move or get crowded.

# UI
- Item Slot UI: A small icon at the bottom of the screen showing the current item and its cooldown.
- Mode Indicator: If the item has multiple modes, show the active one.

# Key Asset & Context
- `RandomRoam.cs`: Modified to handle `Freeze` and `Swarm` states.
- `PlayerMove.cs`: Modified to handle item usage (Input 'Q').
- `ItemSystem.cs` (New): Handles item collection, usage, and networking.
- `ItemData.cs` (New): ScriptableObject for item definitions.

# Implementation Steps
## 1. Prepare AI Logic (RandomRoam.cs)
- Add `public void RPC_SetAIState(int state, int targetViewID, float duration)` to handle remote commands.
- **Dependencies**: None.

## 2. Implement Item System
- Create `Item` base class and `ItemManager`.
- Create `SeekerItem` which calls the AI state change RPCs.
- **Dependencies**: Step 1.

## 3. Update Player Controller (PlayerMove.cs)
- Detect item use input.
- If the player is a Seeker, execute the item effect.
- Sync the effect across the network using Photon.
- **Dependencies**: Step 2.

## 4. UI and Visual Feedback
- Add a simple UI to show item availability.
- Add a visual effect (e.g., a signal flash) when the item is used.
- **Dependencies**: Step 3.

# Verification & Testing
- **Manual Test (Freeze)**: Use the item as Seeker. Verify all AI stop moving. Observe if survivors are easier to find.
- **Manual Test (Swarm)**: Use the item as Seeker. Verify all AI move towards a survivor. Check if the survivor is overwhelmed by dummies.
- **Network Test**: Ensure both Master Client and clients see the same AI behavior when the item is used.
