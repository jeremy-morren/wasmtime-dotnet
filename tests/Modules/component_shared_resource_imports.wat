(component
  (import "demo:poll/types@0.1.0" (instance $poll
    (export "pollable" (type $p (sub resource)))))
  (alias export $poll "pollable" (type $p))
  (import "demo:streams/ops@0.1.0" (instance $streams
    (export "subscribe" (func $subscribe (result (own $p))))))
  (alias export $streams "subscribe" (func $subscribe))
  (core func $subscribe-lowered (canon lower (func $subscribe)))
  (core func $drop (canon resource.drop $p))
  (core module $m
    (import "" "subscribe" (func $subscribe (result i32)))
    (import "" "drop" (func $drop (param i32)))
    (func (export "run")
      (local $p i32)
      (local.set $p (call $subscribe))
      (call $drop (local.get $p))))
  (core instance $imports
    (export "subscribe" (func $subscribe-lowered))
    (export "drop" (func $drop)))
  (core instance $i (instantiate $m (with "" (instance $imports))))
  (type $run-t (func))
  (func $run (type $run-t) (canon lift (core func $i "run")))
  (export "run" (func $run)))