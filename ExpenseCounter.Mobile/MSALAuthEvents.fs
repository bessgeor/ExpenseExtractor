module MSALAuthEvents
  let onAuthRequired = Event<unit>()
  let onAuthSuccess = Event<unit>()