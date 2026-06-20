# Case and Lifecycle

## Six-stage lifecycle

1. **Process Initiation** — Locate beneficiary; collect socio-demographic profile.
2. **Maintain and Development** — ICP, PMA, Case Study Report, Follow-up, Lifeskill Training, Parent Management Training, Group Work, Community Program.
3. **Inter-sectoral Approach** — Legal, Police, Education, Vocational, Psychological/Psychiatric, De-addiction, Material/Financial, Medical support.
4. **Rehabilitation** — Community or institutional (with institution details).
5. **Reintegration**
6. **Termination/Exclusion** — Termination by Court or exclusion by NGO.

## Core case fields (load-bearing)

- Identifiers: Crime Number (unique), ST Number (unique)
- Beneficiary: Name, Age, parent details, address, contact
- Status: studying/working, occupation, education level
- Legal: type of offence, classification (Petty/Serious/Heinous), repeat vs first-time, related cases (cross-links)
- Context: domicile (Urban/Rural/Coastal/Tribal/Slum), family type, economic status (APL/BPL), family crime history, recidivism
- Care: ICP, psychiatric illness flags (counselling required/attended/court-ordered), lifeskill training, community service, PMA status for heinous cases
- Location: GPS lat/long, landmark, verified date/by
- Operations: next visit date, visit count (auto), notes summary, interventions needed/provided links

## Search and filtering

Full-text: crime number, ST number, name, contact, area. Filters: stage, offence type, classification, assigned staff, overdue, area/district. Saved filter presets per user.

## Handoff (CAP-15)

On transfer within seven days: three-line handoff summary at top of case—prior worker actions, open items, next visit purpose. Coordinator may initiate staff bridge when unit pattern is unique to one prior handler.

## Duplicate prevention (CAP-3)

Real-time alert on crime/ST entry; possible-match before save; Coordinator merge workflow.
