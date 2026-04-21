# AegisubDialogDslGen indent-DSL example
# - "columns" is optional; it defaults to the widest row (in grid units).

row:
  label "Hydra (generated)" w=7
  label "Special:"
  dropdown name=special items=["sort_tags","convert_clip"] w=2

row:
  checkbox name=c_on label=\c
  coloralpha name=c
  spacer 1
  checkbox name=alpha_on label=\alpha
  dropdown name=alpha items=["00","80","FF"]

# Left/right groups (right group is right-aligned)
row gap=1:
  left:
    label label="Left group" w=3
  right:
    label label="Right:"
    edit name=value value=abc w=3
