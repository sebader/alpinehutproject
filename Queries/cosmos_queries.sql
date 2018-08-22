SELECT distinct c.name, c.country, c.hutLanguage, a.date, a.rooms FROM c
join a in c.availability
join r in a.rooms
where a.date = '2018-08-25T00:00:00'
and r.freeRoom > 0
and c.hutLanguage <> "de_CH"
and c.country <> "Switzerland"