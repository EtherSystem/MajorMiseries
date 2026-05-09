# Major Miseries

**PLEASE READ THIS README PLEASE READ THIS README PLEASE READ THIS README PLEASE READ THIS README**

I know this Readme is very long, but it's crucial to read it because it will be very easy to lose a save file by dying stupidly because you haven't read what's written here.  
Now that you've been warned, it's your fault, not mine.  

**PLEASE READ THIS README PLEASE READ THIS README PLEASE READ THIS README PLEASE READ THIS README**

Major Miseries is a mod for The Long Dark that adds severe, long-term afflictions, escalating survival pressure, and consequences that can follow a survivor for days, weeks, or even an entire save.

They are fractures, sicknesses, infections, respiratory damage, psychological discomfort, severe sprains, corpse-related illness, and the slow collapse of safety over time.

*Survival should leave marks. Time is only an issue if you are not prepared, but are you prepared enough...?*

ALSO A HUGE THANK YOU TO FLOWER FIELD FOR THESE MAGNIFICENT ICONS!

## Overview

Major Miseries is built around long-term consequences.

Instead of only punishing one bad moment, many systems track what the survivor has been doing over time:

- Spending too long in coal mines can slowly build Black Lung exposure.
- Living near unsafe indoor fires can create Carbon Monoxide danger.
- Repeated sprains on the same joint can escalate into severe injuries.
- Neglecting fatigue, hunger, thirst, warmth, or condition can weaken the Immunity Shield.
- Internal body temperature can drift into fever, sweating, overheating, or severe cold pressure.
- Killing predators can make the world more hostile.
- Remaining away from home, or trapped in a hated region, can wear the survivor down.
- Repeated catastrophic wounds can permanently scar the body.

Major Miseries aims to make each game more demanding and complex, so this mod is not recommended for beginner players.

## Main Systems

Major Miseries currently includes:

- [Requiem Stages](#requiem-stages)
- [Scurvy and Vitamin C Drain](#scurvy-and-vitamin-c-drain)
- [Predator Hostility](#predator-hostility)
- [Broken Arms](#broken-arm)
- [Broken Legs](#broken-leg)
- [Predator Blood Loss conversion](#predator-blood-loss-to-severe-lacerations)
- [Scarred Flesh](#scarred-flesh)
- [Sepsis](#sepsis)
- [Immunity Shield](#immunity-shield)
- [Body Temperature](#body-temperature-and-fever)
- [Fever Response](#fever-response)
- [Black Lung](#black-lung)
- [Carbon Monoxide Exposure and Poisoning](#carbon-monoxide)
- [Corpse Sickness](#corpse-sickness)
- [Severe Wrist Sprains](#severe-wrist-sprain)
- [Severe Ankle Sprains](#severe-ankle-sprain)
- [Home Comfort](#home-comfort)
- [Home Sickness](#home-sickness)
- [Regional Distress](#regional-distress)

Most systems can be enabled, disabled, or tuned through ModSettings.

## Requiem Stages

Requiem Stages are the backbone of Major Miseries.

As the survivor lasts longer, the world begins to weigh harder on them. These stages are based on days survived and apply increasingly severe pressure.

By default, the stages are:

| Stage | Default day survived | Max condition penalty | Locked gauges | Base Predator Threat |
|---|---:|---:|---|---:|
| Omen | 10 days | -10 max condition | Hunger | 1 |
| Dirge | 20 days | -20 max condition | Hunger, Thirst | 2 |
| Knell | 35 days | -30 max condition | Hunger, Thirst, Fatigue | 3 |
| Requiem | 50 days | -50 max condition | Hunger, Thirst, Fatigue, Freezing | 4 |

These thresholds can be customized in the mod settings. The customization toggle only shows or hides the sliders, the stage logic always uses the configured threshold values.

Requiem Stages are not meant to represent a single illness. They represent the long-term degradation of the survivor after too much cold, hunger, trauma, exhaustion, and exposure.

The stage afflictions do not stack with each other. The current highest stage determines the Requiem Stage penalty. Scarred Flesh stacks separately on top of the stage max condition penalty.

### Omen

Signs is the first stage. Omen is the affliction.   
Omen is a harbinger, here to announce the arrival of the end times.

Effects:

| Effect | Value |
|---|---:|
| Starts after | 10 survived days |
| Maximum condition | -10 max condition |
| Hunger | Max hunger reduced to 75% |
| Sleep fatigue recovery | -50% |
| Maximum sleep duration | Vanilla maximum sleep hours -4 hours |
| Base Predator Threat | 1 |
| Predator Hostility | Can start contributing if Predator Hostility mode allows it |


### Dirge

Advent is the second stage. Dirge is the affliction.  
The end times have begun.  

Effects:

| Effect | Value |
|---|---:|
| Starts after | 20 survived days |
| Maximum condition | -20 max condition |
| Hunger | Max hunger reduced to 75% |
| Thirst | Max thirst reduced to 75% |
| Sleep fatigue recovery | -50% |
| Maximum sleep duration | Vanilla maximum sleep hours -4 hours |
| Body temperature | -5°C |
| Base Predator Threat | 2 |


### Knell

Collapse is the third stage. Knell is the affliction.  
Bells toll to signify the arrival of the end.

Effects:

| Effect | Value |
|---|---:|
| Starts after | 35 survived days |
| Maximum condition | -30 max condition |
| Hunger | Max hunger reduced to 75% |
| Thirst | Max thirst reduced to 75% |
| Fatigue | Max fatigue reduced to 75% |
| Sleep fatigue recovery | -50% |
| Maximum sleep duration | Vanilla maximum sleep hours -4 hours |
| Body temperature | -5°C |
| Movement speed | -10% |
| sprint speed | -25% speed while sprinting, unless vanilla Weak Joints is already active |
| Base Predator Threat | 3 |


### Requiem

End of Times is the final stage. Requiem is the affliction.  
The end has passed and leaves behind a grim and desolate world, survival is only temporary.

Effects:

| Effect | Value |
|---|---:|
| Starts after | 50 survived days |
| Maximum condition | -50 max condition |
| Hunger | Max hunger reduced to 75% |
| Thirst | Max thirst reduced to 75% |
| Fatigue | Max fatigue reduced to 75% |
| Freezing | Max cold reduced to 75% |
| Sleep fatigue recovery | -50% |
| Maximum sleep duration | Vanilla maximum sleep hours -4 hours |
| Body temperature | -5°C |
| Movement speed | -10% |
| sprint speed | -25% speed while sprinting, unless vanilla Weak Joints is already active |
| Natural condition recovery | Disabled |
| Willpower condition recovery | Disabled |
| Incoming condition damage | 2x |
| Predator Blood Loss conversion | Enabled by default for Requiem, unless changed in settings |
| Base Predator Threat | 4 |

Note : Requiem is not meant to be fair. That's precisely what I want.

## Scurvy and Vitamin C Drain

Major Miseries can make the vanilla Scurvy system more dangerous by accelerating Vitamin C drain.

It modifies the hidden Vitamin C reserve used by the vanilla Scurvy system, which means Scurvy pressure can arrive sooner if the survivor does not keep eating Vitamin C sources.

Vitamin C Drain can be configured in the Requiem Stages settings.

| Setting | Behavior |
|---|---|
| Only with Requiem Stages | Vitamin C drain is accelerated only once at least one Requiem Stage is active. This is the default mode. |
| Always | Vitamin C drain is accelerated even before Requiem Stages begin or are disabled. |
| Disabled | Vitamin C drain is not modified by Major Miseries. |

When Vitamin C Drain is active, the selected preset multiplies the vanilla Vitamin C loss.

| Preset | Vitamin C drain multiplier |
|---|---:|
| Forgiving | 1.5x |
| Standard | 2.0x |
| Harsh | 3.0x |
| Brutal | 4.0x |
| Who Wants to Play Like This? | 5.0x |

The default preset is Standard, so by default active Vitamin C drain is doubled.

## Scarred Flesh and Maximum Condition

Scarred Flesh stacks with Requiem Stage condition penalties.

| Source | Max condition penalty |
|---|---:|
| Omen | -10 |
| Dirge | -20 |
| Knell | -30 |
| Requiem | -50 |
| Each Scarred Flesh history stack | -2 |

Scarred Flesh is not a temporary injury. It records healed Severe Laceration trauma and keeps applying its penalty through the save.

## Predator Hostility

Predator Hostility is a persistent threat system linked to predator kills.

When enabled, killing predators increases a hidden hostility value. This value contributes to Dynamic Predator Threat, making predators more dangerous over time.

Default hostility gains are:

| Predator killed | Hostility gained |
|---|---:|
| Wolf | +1 |
| Moose | +2 |
| Bear | +3 |
| Cougar | +4 |

This means killing a cougar has a much larger long-term impact than killing a wolf for... Obvious reasons.

Predator Hostility contributes to Dynamic Predator Threat as follows:

| Current Predator Hostility | Dynamic Predator Threat |
|---:|---:|
| 0 to less than 3 | 0 |
| 3 to less than 6 | 1 |
| 6 to less than 9 | 2 |
| 9 to less than 12 | 3 |
| 12 to less than 16 | 4 |
| 16 to less than 20 | 5 |
| 20 to less than 25 | 6 |
| 25 to less than 30 | 7 |
| 30 or more | 8 |

Total Predator Threat is:

```text
Total Predator Threat = Base Predator Threat + Dynamic Predator Threat
```

Base Predator Threat comes from the current Requiem Stage:

| Stage | Base Predator Threat |
|---|---:|
| No stage | 0 |
| Omen | 1 |
| Dirge | 2 |
| Knell | 3 |
| Requiem | 4 |

Predator Threat scaling uses this multiplier:

```text
Threat multiplier = 1 + (0.2 × Total Predator Threat)
```

Examples:

| Total Predator Threat | Multiplier |
|---:|---:|
| 1 | 1.2x |
| 2 | 1.4x |
| 4 | 1.8x |
| 6 | 2.2x |
| 8 | 2.6x |
| 10 | 3.0x |
| 12 | 3.4x |

Predator Threat alters the overall behavior of predators: they will smells you, stalk you, and attack you up to the multiplier's range.  
For example, a bear can sense you from over 500 meters away and attack you from 85 meters away at the maximum Predator Threat level.


Predator Hostility decay:

| Behavior | Value |
|---|---:|
| Delay before decay starts | 168 hours / 7 days after the last predator kill |
| Decay rate | -2 hostility per day |
| Decay rate per hour | About -0.083 hostility per hour |

The result is that all scenes will become increasingly dangerous if you systematically resolve every encounter with a predator through violence.

### Predator Hostility Modes

Predator Hostility can be configured to work in different ways:

| Mode | Behavior |
|---|---|
| Only with Requiem Stages | Predator Hostility only matters once at least one Requiem Stage is active. If no stage is active yet, predator kills do not build hostility. |
| Always | Predator kills can build hostility regardless of Requiem Stage. |
| Disabled | Predator Hostility is disabled. |

The intended default experience is to connect Predator Hostility with the long-term pressure of Requiem Stages.

## Broken Arm

Broken Arm is a severe predator-related injury.

It can occur after major predator trauma, especially from large predators.

Default trigger values:

| Source | Default chance |
|---|---:|
| Bear struggle | 20% |
| Moose struggle | 30% |
| If player fatigue is 40 or lower | +10 percentage points |
| If double broken limbs are disabled | One random broken limb is applied |
| If double broken limbs are enabled | One broken arm and one broken leg are both applied |

Broken Arm duration:

| Duration mode | Duration |
|---|---:|
| Realistic | 1008 to 1344 hours / 42 to 56 days |
| Unrealistic | 100.8 to 134.4 hours / 4.2 to 5.6 days |

Keep in mind that Broken Arm is designed to use the Realistic preset.

Effects:

| Effect | One broken arm | Two broken arms |
|---|---:|---:|
| Rope climbing | Blocked | Blocked |
| Crafting time | 1.5x | 2.0x |
| Aim sway increase speed | 2.0x | 3.0x |
| Aim sway decrease/recovery speed | 0.65x | 0.45x |

A broken arm does not just reduce condition. It changes what the player can safely do afterward. Rope routes become unusable, crafting becomes more expensive in time, and combat becomes less reliable.

## Broken Leg

Broken Leg is one of the most punishing injuries in Major Miseries.

It can occur after predator trauma or a hard fall.

Default trigger values from predator struggles:

| Source | Default chance |
|---|---:|
| Bear struggle | 20% |
| Moose struggle | 30% |
| If player fatigue is 40 or lower | +10 percentage points |
| If double broken limbs are disabled | One random broken limb is applied |
| If double broken limbs are enabled | One broken arm and one broken leg are both applied |

Broken Leg from fall damage:

| Fall condition loss | Result |
|---|---|
| Less than 15% condition lost | No broken leg roll |
| 15% to less than 25% condition lost | 35% chance to break a leg |
| 25% condition lost or more | Guaranteed broken leg |

Broken Leg duration:

| Duration mode | Duration |
|---|---:|
| Realistic | 1008 to 2016 hours / 42 to 84 days |
| Unrealistic | 100.8 to 201.6 hours / 4.2 to 8.4 days |

Keep in mind that Broken Leg is designed to use the Realistic preset.

Effects:

| Effect | One broken leg | Two broken legs |
|---|---:|---:|
| Sprinting | Blocked | Blocked |
| Rope climbing | Blocked | Blocked |
| Movement speed | 0.65x | 0.45x |
| Movement fatigue | 1.5x | 2.0x |
| Carry capacity and encumbrance thresholds | 0.75x | 0.50x |

Broken Leg is designed to make bad movement decisions matter. A survivor with a broken leg may still live, but travel, escape, hauling, and shelter decisions become much more dangerous.

## Predator Blood Loss to Severe Lacerations

Major Miseries can convert predator-caused Blood Loss into vanilla Severe Lacerations.

This can be configured to:

| Setting | Behavior |
|---|---|
| Only with Requiem | Predator Blood Loss can become Severe Lacerations only during Requiem. This is the default behavior. |
| Always | Predator Blood Loss can become Severe Lacerations at any time. |
| Disabled | This conversion is disabled. |

Keep in mind that if you're playing with the "Only With Requiem" preset, your maximum condition will already be reduced by 50%, and Severe Lacerations also reduces your maximum condition by 50%, resulting in instant death. This is intentional and the only way to survive that is with the well fed buff.

## Scarred Flesh

Scarred Flesh represents permanent damage left behind after Severe Lacerations heal.

When Severe Lacerations are healed, Major Miseries records that trauma and increases Scarred Flesh history by 1.

One Scarred Flesh reduce max condition by 2%.

Scarred Flesh isn't designed to kill the player instantly. Its purpose is to have a long-term impact on the catastrophic injuries that Severe Lacerations are.  
A survivor who survives terrible injuries is still alive, but it leaves its mark.

## Sepsis

Sepsis expands the danger of untreated infection.

### Sepsis Risk

If a vanilla Infection remains untreated, it can begin progressing toward Sepsis Risk.

Default behavior:

| Effect | Value |
|---|---:|
| Trigger | Vanilla Infection starts |
| Base risk gain while matching infection remains untreated | +25 risk per hour before Immunity Shield modifiers |
| Time from 0 to 100 risk at normal immunity | 4 hours |
| Immunity Shield interaction | Strong shield slows Sepsis Risk, weak shield accelerates it |
| Antibiotics taken for the matching vanilla infection | Cures Sepsis Risk |
| If the matching vanilla infection disappears | Cures Sepsis Risk |


### Sepsis

If Sepsis develops, that means the infection has spread through the bloodstream.

Default effects:

| Effect | Value |
|---|---:|
| Total duration | 480 hours / 20 days |
| Condition loss while untreated | -10 condition per hour |
| Condition loss while treated | -0.2 condition per hour |
| Antibiotics required for treatment display | 4 antibiotics total |
| Treatment structure | 2 doses of 2 antibiotics |
| Treatment suppression duration | 120 hours / 5 days |
| Natural condition recovery | Disabled while Sepsis is active |

Antibiotics can suppress the worst symptoms, but the affliction still has to be managed carefully over time.

## Immunity Shield

The Immunity Shield is a hidden long-term resistance system displayed in the First Aid panel when enabled.

It represents how much biological resilience the survivor still has left.

The shield ranges from 0% to 100%.

### What drains the Immunity Shield

The shield drains when the survivor is in poor physical condition or under biological pressure.

Default body pressure behavior:

| Source | Shield behavior |
|---|---|
| Energy below 25% | Drains the shield |
| Calories below 25% | Drains the shield |
| Hydration below 25% | Drains the shield |
| Warmth below 25% | Drains the shield |
| Any of these stats at 0% | Makes shield drain harsher |

The lower the stat is below 25%, the stronger the drain becomes. A stat barely under 25% is mild. A stat near 0% is much worse.

Biological threats also drain the shield:

| Active affliction | Base shield drain |
|---|---|
| Infection Risk | -0.25 / hour |
| Infection | -0.75 / hour |
| Intestinal Parasites Risk | -0.20 / hour |
| Intestinal Parasites | -0.50 / hour |
| Food Poisoning | -0.50 / hour |
| Dysentery | -0.75 / hour |
| Sepsis Risk | -1.00 / hour |
| Sepsis | -2.00 / hour |
| Corpse Sickness Risk | -0.40 / hour |
| Corpse Sickness | -0.80 / hour |

If multiple biological threats are present, the system only considers the highest threat.  

If the survivor is both physically depleted and biologically threatened, the shield drains faster.

### What restores the Immunity Shield

The shield can recover only when the survivor is stable.

Default recovery requirements:

| Requirement | Value |
|---|---:|
| No active biological threat | Required |
| Energy | Above 25% |
| Calories | Above 25% |
| Hydration | Above 25% |
| Warmth | Above 25% |
| Condition, default setting | Above 50% |
| Condition, relaxed setting | Above 20% |

Default recovery speed:

| State | Base shield recovery |
|---|---:|
| Awake | +0.10 shield per hour |
| Sleeping | +1.00 shield per hour |

High energy, calories, hydration, warmth, and condition slightly improve recovery. Home Comfort also slightly improves Immunity Shield recovery.

### What the Immunity Shield changes

The Immunity Shield affects infection-style risks.

| Shield state | Risk behavior |
|---|---|
| 100% shield | Infection-style risks progress much slower |
| 50% shield | Vanilla risk speed |
| 0% shield | Infection-style risks can progress up to 4x faster |

This affects vanilla Infection Risk, vanilla Intestinal Parasites Risk, and Sepsis Risk.

It does not replace medicine. A strong shield helps the survivor resist problems, but a bad situation can still get worse.

## Body Temperature and Fever

Major Miseries adds an internal body temperature system displayed in the First Aid panel when enabled.

This is separate from the vanilla freezing meter. The freezing meter still matters, but Body Temperature tracks the survivor's internal heat directly.

Default normal body temperature is around 37°C.

### Body Temperature

Body Temperature is affected by cold exposure, warm ambient conditions, nearby fires, movement, fever, and severe overheating.

Default warming and cooling settings:

| Source | Default value |
|---|---:|
| Environmental warming | Up to +2.5°C per hour in warm conditions |
| Walking warming | +0.25°C per hour |
| Encumbered warming | +1.25°C per hour |
| Sprinting warming | +5°C per hour |
| Climbing warming | +10°C per hour |
| Cold exposure cooling | Base -0.5°C per hour before severe cold multipliers |
| Nearby fire warmth | Can warm body temperature directly |

Movement heat only helps while the survivor is not already deeply freezing, and sprinting or climbing will not keep pushing body temperature forever.

Cold ambient conditions increase cooling. If the warmth meter is very low, cooling becomes much stronger.

### Sweating, hydration, and overheating

When internal body temperature rises above normal (precisely above 37.2°C), you will begin sweating.

Sweating slowly adds wetness to worn clothing. The hotter the survivor gets, the stronger the sweating becomes.

High internal body temperature also increases hydration consumption. This is not specific to Fever. Any source of body heat can increase hydration pressure if the survivor gets hot enough.

| Body temperature | Hydration consumption |
|---|---:|
| 38°C or higher | 1.0x |
| 39°C or higher | 1.5x |
| 40°C or higher | 2.0x |
| 41°C or higher | 2.5x |
| 42°C or higher | 3.0x |
| 43°C | 4.0x |

Severe overheating also has direct effects:

| Body temperature | Effect |
|---|---|
| Above 40°C | Heat headache camera pulses can begin |
| Above 42°C | Movement staggering can begin |
| 43°C | Maximum internal body temperature |

### Fever Response

Fever is part of the Immunity Shield system.

When the survivor has at least 50% Immunity Shield and is fighting a biological threat that can cause fever, fever can appear after about one in-game hour. Fever raises body temperature and increases fatigue pressure. Hydration pressure comes from the survivor's actual internal body temperature, not directly from Fever itself.

Fever severity is based on the fever target temperature caused by the current biological threat. If multiple fever sources are active at the same time, the strongest fever target is used.

| Fever source | Fever target | Fever state | Fatigue pressure |
|---|---:|---|---:|
| Intestinal Parasites | 38°C | Moderate Fever | 2.0x |
| Corpse Sickness | 38°C | Moderate Fever | 2.0x |
| Infection Risk | 39°C | Moderate Fever | 2.0x |
| Food Poisoning | 40°C | Severe Fever | 2.5x |
| Infection | 41°C | Severe Fever | 2.5x |
| Dysentery | 41°C | Severe Fever | 2.5x |
| Sepsis | 43°C | Critical Fever | 3.0x |

In practical terms, Moderate Fever means the body is responding to an early or lighter biological problem, Severe Fever means the survivor is fighting a serious active illness, and Critical Fever is reserved for Sepsis-level danger.

Lingering Fever can remain for up to 3 in-game hours after the active fever source stops being valid. During this fading period, fatigue pressure is 1.25x.

Active fever slightly helps resist infection-style risk progression. With a very strong Immunity Shield, fever can sometimes contain vanilla Infection Risk.

Cold can suppress fever temperature. In other words, fever does not make the survivor immune to freezing.

## Black Lung

Black Lung is caused by long-term exposure to coal-heavy locations such as coal caves, mines, and similar scenes.

The system uses hidden exposure before the player sees an affliction.

This means the danger builds quietly at first.

### Black Lung Exposure

While the player remains in a coal-heavy scene, hidden Black Lung Exposure increases.

Default exposure behavior:

| Value | Default |
|---|---:|
| Exposure target before risk | 75 |
| Time to reach 75 exposure | 336 hours / 14 days |
| Exposure gain rate in coal scenes | About +0.223 exposure per hour |
| Exposure decay outside coal scenes | About -0.056 exposure per hour |
| Time to decay from 75 to 0 outside coal scenes | 1344 hours / 56 days |

Leaving coal-heavy scenes allows exposure to slowly decrease, but recovery is 4x slower than exposure buildup.

This makes short visits relatively safe, but long-term living or repeated extended stays in coal-heavy places dangerous.

### Black Lung Risk

When Black Lung Exposure reaches its threshold, Black Lung Risk begins.

Default risk behavior:

| Value | Default |
|---|---:|
| Time to reach full risk while exposed | 112 hours / 4 days and 16 hours |
| Risk gain per hour while exposed | About +0.893 risk per hour |
| Risk decay outside coal scenes | About -0.223 risk per hour |
| Time to decay from 100 to 0 outside coal scenes | 448 hours / 18.7 days |

If Black Lung Risk reaches 100, it becomes Black Lung.

Black Lung exposure and Black Lung Risk can be avoided by wearing a respirator with active protection. Blocking coal exposure consumes canister condition slowly.

### Black Lung

Black Lung is a severe respiratory illness.

Default effects:

| Effect | Realistic duration mode | Unrealistic duration mode |
|---|---:|---:|
| Black Lung duration | 3600 hours / 150 days | 360 hours / 15 days |

Other effects:

| Effect | Value |
|---|---:|
| Sprint stamina usage | 1.5x |
| Sprint stamina recovery | -50% |
| Delay before sprint stamina recovery | 1.5x |
| Sleep recovery against Black Lung duration | Each hour slept removes 10 hours from remaining duration |
| Coal-scene worsening while Black Lung is active | Each hour in coal exposure adds 10 hours to remaining duration |
| Sleep cough interval | Randomly every 2 to 4 hours of sleep |

BlackLung is a severe affliction, but in a more vicious way than others, it is not violent through a strong condition drain or any effect of that style, but is severe because it continually disrupts the sleep cycle.

Avoiding further exposure and getting proper rest can help recovery.

The idea is simple: coal-heavy shelters are useful, but living in them too long should have a cost.

## Carbon Monoxide

Carbon Monoxide is tied to unsafe indoor fire use.

It is not meant to make every indoor fire dangerous. It is meant to punish careless or unsafe fires burning too long in the wrong places.

### Carbon Monoxide Exposure

Carbon Monoxide danger can begin in indoor scenes when an unsafe fire has been burning for long enough.

Once a qualifying unsafe fire has burned for more than two in-game hours, Major Miseries begins checking for Carbon Monoxide danger every 10 in-game minutes. Each eligible unsafe fire adds 10% roll chance, capped at 100%. If the roll succeeds, the indoor scene will be considered contaminated as long as the fire is burning + 2 hours.

If exposure starts, the player has 30 in-game minutes before it becomes Carbon Monoxide Poisoning.

Leaving the contaminated indoor scene stops the active exposure before it becomes poisoning.

Carbon Monoxide Exposure can be avoided by wearing a respirator with active protection. Blocking Carbon Monoxide consumes canister condition faster than Black Lung protection.

Safe stoves and proper chimney-style fireplaces are designed to avoid false positives as much as possible (it is entirely possible that there are exceptions not taken into account, any feedback is appreciated !).

### Carbon Monoxide Poisoning

If Carbon Monoxide Exposure is ignored, it develops into Carbon Monoxide Poisoning.

Default effects:

| Effect | Value |
|---|---:|
| Duration | Random 6 to 24 hours |
| Condition loss | -5 condition per hour |
| Fatigue increase | 3.0x fatigue increase |
| Sprint stamina usage | 2.0x |
| Sprint stamina recovery | -70% |
| Delay before sprint stamina recovery | 2.0x |
| Willpower-style condition recovery | Disabled while CO Poisoning is active |

Carbon Monoxide is meant to make indoor fire safety matter.

A survivor can still use fire indoors, but careless long burns in unsafe places can become deadly.

## Corpse Sickness

Corpse Sickness is caused by spending too much time near human corpses or animal carcasses.

The system tracks hidden Corpse Exposure while the player remains close to valid exposure sources.

By default, both human corpses and animal carcasses can contribute.

### Corpse Exposure

Corpse Exposure is hidden at first.

The player does not immediately get sick from walking past a corpse or harvesting one carcass. The danger comes from repeated or prolonged exposure.

Default exposure behavior:

| Value | Default |
|---|---:|
| Human corpse detection radius | 15 m |
| Animal carcass detection radius | 10 m |
| Time to reach 100 exposure near a human corpse | 1 hour |
| Human corpse exposure gain | +100 exposure per hour |
| Time to reach 100 exposure near an animal carcass | 5 hours |
| Animal carcass exposure gain | +20 exposure per hour |
| Exposure decay while away, both source types enabled | -10 exposure per hour |
| Time to decay from 100 to 0, both source types enabled | 10 hours |

Decay depends on enabled source types:

| Enabled exposure source | Exposure decay while away |
|---|---:|
| Human corpses only | -50 exposure per hour |
| Animal carcasses only | -10 exposure per hour |
| Human corpses and animal carcasses | -10 exposure per hour |

This is especially relevant when:

- A corpse is near a shelter.
- Animal carcasses are kept nearby for a long time.
- The player spends many hours harvesting near corpses.
- A location becomes a long-term camp surrounded by death.

Small carried or harvested carcasses such as rabbits and ptarmigans are ignored by the corpse sickness animal carcass logic.

If there are two or more sources of exposure close to the player, they do not accumulate, but the system will retain the strongest one.

### Corpse Sickness Risk

If hidden Corpse Exposure reaches its maximum, Corpse Sickness Risk begins.

Default risk behavior:

| Value | Default |
|---|---:|
| O to 100 risk time | 12 hours |
| 100 to 0 risk time | 24 hours |

The risk continues increasing while the player remains near corpses or carcasses.

It decreases when the player gets away from exposure sources.

### Corpse Sickness

If the risk reaches its maximum, it becomes Corpse Sickness.

Default effects:

| Effect | Value |
|---|---:|
| Duration | Random 48 to 96 hours |
| Condition loss | -1.5 condition per hour |
| Fatigue increase | 2.0x fatigue increase |
| Willpower condition recovery | Disabled while Corpse Sickness is active |
| Corpse Exposure after cure | Reset to 0 |

When Corpse Sickness is active, the Willpower condition regeneration is disabled. When Corpse Sickness is cured, the hidden exposure is reset to 0.

Corpse Sickness exists to make long-term corpse proximity feel unhealthy and unsafe.
The base idea is that it's a psychological affliction, being surrounded by death makes you feel bad.

## Severe Sprains

Severe Sprains expand the vanilla sprain system.

Repeated sprains on the same joint can create a Severe Sprain Risk.

If another sprain happens while the risk is active, the vanilla sprain is converted into a severe injury instead.

The system tracks each joint separately:
Left wrist / Right wrist / Left ankle / Right ankle

This means repeated injuries to the same joint matter more than random isolated sprains.

### Severe Sprain Risk

With most presets, a Severe Sprain Risk appears when the player suffers too many sprains on the same joint within the preset tracking window.

The risk does not immediately become a severe sprain. Instead, it means the joint is now vulnerable. Another sprain on that same joint while the risk is active can become a severe injury.

When Severe Sprain Risk starts, it starts at 99% and then decays over the preset risk duration.

Default preset values:

| Preset | Sprains required on same joint | Tracking window | Risk duration | Severe sprain duration |
|---|---:|---:|---:|---:|
| Forgiving | 3 | 120 hours / 5 days | 18 hours | 48 hours |
| Standard | 2 | 72 hours / 3 days | 24 hours | 72 hours |
| Harsh | 2 | 96 hours / 4 days | 36 hours | 96 hours |
| Brutal | 1 | 120 hours / 5 days | 48 hours | 120 hours |
| Who Wants to Play Like This? | Every sprain converts directly | - - - - - - - - - - - - | - - - - - - - - - | 240 hours |

The "Who Wants to Play Like This?" preset skips the risk step entirely. Any vanilla sprain becomes a Severe Sprain directly.

### Severe Wrist Sprain

Severe Wrist Sprain affects weapon handling, crafting, and rope climbing.

Default effects:

| Effect | One severe wrist sprain | Two severe wrist sprains |
|---|---:|---:|
| Duration | Based on preset | Based on preset |
| Rope climbing | Blocked | Blocked |
| Crafting time | 1.35x | 1.35x |
| Aim sway increase speed | 2.5x | 2.5x |
| Aim sway decrease/recovery speed | 0.5x | 0.5x |
| Two-handed weapons | Blocked | Blocked |
| One-handed weapons | Allowed | Blocked |

Two-handed weapons include:

- Rifles
- Revolvers
- Bows
- Shotgun

### Severe Ankle Sprain

Severe Ankle Sprain affects movement.

Default effects:

| Effect | Value |
|---|---:|
| Duration | Based on selected Severe Sprain preset |
| Sprinting | Blocked |
| Rope climbing | Blocked |
| Movement speed | -25% |
| Movement fatigue | 1.35x |

## Regional Afflictions

Major Miseries adds region-based comfort and discomfort systems.

These systems are built around the idea that a survivor can become attached to one region, or feel deeply uncomfortable in another.   

*(Like... Forlorn Muskeg, for example. Just an example... I have nothing against that particular region, it could have been... I don't know... another region or something... well, it just so happens that's the one I thought of, bad luck I'd say... Don't get me wrong, I have absolutely nothing against flat and boring regions, I know a particularly flat and boring one myself and I sometimes go there... Of course, comparatively speaking, Mystery Lake is clearly a more interesting region, but hey, who am I to compare the value of one region to another ? You know, I don't believe there are good or bad regions. If I had to summarize my experience on TLD today with you, I'd say it's primarily about encounters. People who reached out to me, perhaps at a time when I couldn't, when I was alone at home. And it's quite curious to think that chances, encounters shape a destiny... Because when you have a taste for ... things, when you have a taste for a job well done, for a beautiful thing, sometimes you don't find the right person to talk to, the mirror that helps you move forward... Well, that's not my case, as I was saying, because on the contrary, I was able to; and I thank life, I thank it, I sing of life, I dance for life... I am nothing but love ! And finally, when people ask me, "But how do you manage to have such humanity ?", I answer them very simply that it's this taste for love, this taste that has driven me today to undertake the development of MajorMiseries... But who knows what tomorrow will bring ? Perhaps simply to put myself at the service of the community, to make the gift... um... the gift of myself... hmm, anyway)*.

### Home Comfort

Home Comfort is a small buff gained while the player is in their configured home region.

Default effects:

| Effect | Value |
|---|---:|
| Movement fatigue multiplier | 0.95x |
| Sleep recovery multiplier | 1.05x |
| Feels-like temperature | +1°C |
| Immunity Shield recovery | 1.05x, if Immunity Shield is enabled |

This means the survivor uses about 5% less movement fatigue, recovers about 5% more fatigue from sleep, feels slightly warmer, and recovers Immunity Shield a little faster while in their home region.

It is meant to make a chosen home region feel slightly more familiar and easier to endure.  
*(Like... Mystery Lake, for example. Again its just an example... hrm anyway)*.

Changing the configured Home Region starts a 30-day relocation cooldown before it can be changed again.

### Home Sickness

Home Sickness builds when the player spends too much time away from their configured home region.

Default behavior:

| Behavior | Value |
|---|---:|
| Default delay before Home Sickness | 72 hours away from home |
| Configurable delay range | 1 to 336 hours |
| Timer starts increasing when | Player is outside the configured home region |
| Recovery starts when | Player returns to the configured home region |
| Recovery speed after returning home | 4x faster |
| Movement fatigue multiplier while active | 1.15x |
| Sleep recovery multiplier while active | 0.90x |

Home Sickness is meant to make long expeditions away from a chosen home region feel emotionally and physically draining.

It does not punish short trips. It punishes being away too long.

### Regional Distress

Regional Distress builds when the player spends too much time in a configured disliked region.

Default behavior:

| Behavior | Value |
|---|---:|
| Default delay before Regional Distress | 72 hours in the distress region |
| Configurable delay range | 1 to 336 hours |
| Timer starts increasing when | Player is inside the configured distress region |
| Recovery starts when | Player leaves the configured distress region |
| Recovery speed after leaving disliked region | 4x faster |
| Movement fatigue multiplier while active | 1.10x |
| Sleep recovery multiplier while active | 0.95x |

Regional Distress is similar to Home Sickness, but inverted.

Home Sickness punishes being away from home too long.

Regional Distress punishes staying in one hated region too long.

If the same region is selected as both the home region and the distress region, Regional Distress will not affect the home region.

### Supported Regions

Regional Afflictions support vanilla major regions, several transition regions, Far Territory regions, and optional TLDev regions.

TLDev region support can be a bit clunky, I didnt tested it, all feedbacks is appreciated !

## Compatibility Notes

Major Miseries touches several important gameplay systems.

There may be overlap and strange behavior with other mods that modify:

- Status bar behavior or visuals.
- First Aid panel visuals.
- Hunger, thirst, fatigue, freezing, or body temperature behavior.
- Clothing wetness or overheating behavior.
- Rope climbing.
- Vanilla infection and infection risk.
- Vanilla intestinal parasites risk.
- Vanilla sprains.
- Severe Lacerations.
- Blood Loss.
- Respirator or canister behavior.
- Wildlife AI behavior.
- Fire behavior.

This doesn't mean these mods are destined to malfunction.
It simply means these are the areas most likely to overlap and cause strange, unintended behavior.

## For Developers

<details>
<summary><strong>Click to Expand</strong></summary>

### Developer Console

The following debug commands are available via the [Developer Console](https://github.com/DigitalzombieTLD/TLD-Developer-Console/).

These commands are intended for development, debugging, and controlled save testing.

---

### Requiem Stage Commands

**omen**  
Applies Omen.

**dirge**  
Applies Dirge.

**knell**  
Applies Knell.

**requiem**  
Applies Requiem.

---

### Scarred Flesh Commands

**scarredflesh**  
Increases Scarred Flesh history by 1 and refreshes the Scarred Flesh display.

**reset_SFhistory**  
Resets Scarred Flesh history to 0.

**set_SFhistory [value]**  
Sets Scarred Flesh history to the provided value.

---

### Sepsis Commands

**sepsisrisk**  
Applies Sepsis Risk.

**sepsis**  
Applies Sepsis.

---

### Broken Limb Commands

**brokenleg**  
Applies Broken Leg to a random leg. Uses 2016h in Realistic mode or 201.6h in Unrealistic mode.

**brokenarm**  
Applies Broken Arm to a random arm. Uses 1344h in Realistic mode or 134.4h in Unrealistic mode.

---

### Black Lung Commands

**blacklungrisk**  
Applies Black Lung Risk.

**blacklung**  
Applies Black Lung. Uses 3600h in Realistic mode or 360h in Unrealistic mode.

---

### Carbon Monoxide Commands

**coexposure**  
Applies Carbon Monoxide Exposure.

**copoisoning**  
Applies Carbon Monoxide Poisoning for a random 6h to 24h duration.

---

### Corpse Sickness Commands

**corpsesicknessrisk**  
Applies Corpse Sickness Risk.

**corpsesickness**  
Applies Corpse Sickness for a random 48h to 96h duration.

**reset_CE**  
Resets hidden Corpse Exposure to 0.

**set_CE [value]**  
Sets hidden Corpse Exposure to the provided value.

---

### Severe Sprain Risk Commands

**mm_sprain_risk_wrist_left**  
Applies Severe Wrist Sprain Risk to the left wrist.

**mm_sprain_risk_wrist_right**  
Applies Severe Wrist Sprain Risk to the right wrist.

**mm_sprain_risk_ankle_left**  
Applies Severe Ankle Sprain Risk to the left ankle.

**mm_sprain_risk_ankle_right**  
Applies Severe Ankle Sprain Risk to the right ankle.

---

### Severe Sprain Commands

**mm_severe_sprain_wrist_left**  
Applies Severe Wrist Sprain to the left wrist.

**mm_severe_sprain_wrist_right**  
Applies Severe Wrist Sprain to the right wrist.

**mm_severe_sprain_ankle_left**  
Applies Severe Ankle Sprain to the left ankle.

**mm_severe_sprain_ankle_right**  
Applies Severe Ankle Sprain to the right ankle.

---

### Regional Affliction Commands

**homesickness**  
Applies Home Sickness.

**regionaldistress**  
Applies Regional Distress.

**homecomfort**  
Applies Home Comfort.

**homecomfort_cure**  
Cures Home Comfort.

**reset_HR_timer**  
Resets the Home Region relocation cooldown.

---

### Predator Hostility Commands

**reset_PH**  
Resets Predator Hostility and predator kill decay timer.

**set_PH [value]**  
Sets Predator Hostility to the provided value.

---

### Immunity Shield and Body Temperature Commands

**set_IS [value]**  
Sets Immunity Shield to the provided value, clamped from 0 to 100.

**set_BT [value]**  
Sets internal Body Temperature to the provided value, clamped from 34°C to 43°C.

---

### Batch Commands

**maj_afflictionsrisk**  
Applies the main Major Miseries risk afflictions in debug mode at 50% risk.

**maj_afflictions**  
Applies the main Major Miseries afflictions.

**maj_afflictions_cure**  
Cures all Major Miseries afflictions and buffs currently active.

---

### Vanilla Helper Commands

These commands help test Major Miseries interactions with vanilla afflictions.

**severeL**  
Applies vanilla Severe Lacerations.

**severeL_cure**  
Cures vanilla Severe Lacerations.

**infection**  
Applies vanilla Infection.

**infection_cure**  
Cures vanilla Infection.

---

### Internal Test Command

**mm_testpopup**  
Displays a Requiem-style test popup.

</details>

## Installation

2. Install the required dependencies:
    [AfflictionComponent](https://github.com/TLD-Mods/AfflictionComponent), [ModComponent](https://github.com/dommrogers/ModComponent), [ModSettings](https://github.com/DigitalzombieTLD/ModSettings/) and [ModData](https://github.com/dommrogers/ModData)
3. Place `MajorMiseries.dll` inside your `Mods` folder.
