# Lift-Simulation

This project consists of a variety of C# console apps, each of which simulates a lift serving passengers in a ten-storey building.  

Each app takes as input exactly one string, corresponding to a csv file detailing passenger calls for the lift to serve. In this csv, each row represents a passenger. There are four columns: ID; start time; start floor; and destination floor. Once the simulation has run, the program outputs to the console the total time taken for the lift to serve all passengers. It also generates a csv log containing all recorded events that occurred during the simulation. The following events are logged: a lift state change; the lift arriving at a floor; a passenger collection completion; and a passenger drop off completion. The exact nature of a log entry for a given app depends on the algorithm being simulated by the app. However, for any app, each log entry includes at least: the time of the event; the floor of the lift at that time; the nature of the event; and a list of the IDs of passengers in the lift.

Each app has three sections:
1)	Handle the input csv
2)	Run the simulation
3)	Handle reporting

Each app assumes the lift: (i) starts at floor 1; (ii) takes 10 seconds to move between floors; (iii) takes 5 seconds to collect a passenger; and (iv) takes 5 seconds to drop a passenger. Those apps that consider capacity assume the lift (v) has a capacity of 8. Each of (i)-(v) can be adjusted by passing custom int values into the Lift constructor when the Lift object is created in Main().

---

The most basic app is OpportunisticFIFOWithoutCapacity:

This app simulates a lift that can accommodate multiple passengers at once, but which has no capacity. The lift algorithm sets a target passenger with a First In priority selection protocol, and focuses on serving the target passenger. However, on the way to collecting and dropping the target passenger, at each floor, the lift collects and drops extra passengers that need to be collected/dropped at that floor. Speaking broadly: at a new floor, the priority of actions is as follows (ordered from highest to lowest priority):

(1) Drop the target passenger (if they are inside the lift and their destination floor has been reached).
(2) Drop extra internal passengers.
(3) Collect the target passenger (if they are external and their start floor has been reached).
(4) Collect extra external passengers.

The following events are logged: a lift state change; the lift arriving at a floor; a passenger collection completion; and a passenger drop off completion. Each log entry states: the time of the event, the floor of the lift at that time, the nature of the event, the ID of the passenger being served (0 in the case of no one), and a list of the IDs of passengers in the lift.

---

OpportunisticFIFOWithCapacity builds on OpportunisticFIFOWithoutCapacity:

The lift alogrithm is as before, except now the lift has a capacity. Capacity comes into play at two points:

 (A) The lift only collects extra external passengers if it has capacity.
 (B) If (i) the target passenger is outside the lift, (ii) the lift arrives at the target passenger's start floor and would otherwise start collecting them, and (iii) the lift is at capacity, then the lift moves into an emergency state. In this state, the lift moves to the closest destination floor of a current internal passenger, then drops them off, then immediately returns to the target passenger's start floor, and collects the target passenger.
 
Logging is the same as with OpportunisticFIFOWithoutCapacity.

---

DirectionalWithCapacity is an alternative to OpportunisticFIFOWithCapacity:

This app simulates a lift that can accommodate multiple passengers at once, and which has a capacity. Broadly speaking, the lift serves all calls in one direction (all internal, and external when it has capacity). When there are no more internal calls, or external calls which the lift has capacity for, in its current direction, it changes direction. The app involves notable adjustments to my earlier Lift class, and the introduction of an additional enum.

The following events are logged: a lift state change; the lift arriving at a floor; a passenger collection completion; and a passenger drop off completion. Each log entry states: the time of the event, the floor of the lift at that time, the nature of the event, the direction of the lift, and a list of the IDs of passengers in the lift.








