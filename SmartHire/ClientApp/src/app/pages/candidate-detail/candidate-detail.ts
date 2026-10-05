import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { CandidateDetail as CandidateDetailDto, CandidatesService } from '../../core/services/candidates.service';

@Component({
  selector: 'app-candidate-detail',
  imports: [CommonModule],
  templateUrl: './candidate-detail.html',
  styleUrl: './candidate-detail.scss',
})
export class CandidateDetail implements OnInit {
  readonly candidate = signal<CandidateDetailDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor(
    private readonly route: ActivatedRoute,
    private readonly candidatesService: CandidatesService,
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.candidatesService.getById(id).subscribe({
      next: (candidate) => {
        this.candidate.set(candidate);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load candidate.');
        this.loading.set(false);
      },
    });
  }
}
