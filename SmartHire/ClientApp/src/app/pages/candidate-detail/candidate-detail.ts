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
  readonly actionLoading = signal<string | null>(null);
  readonly actionSuccess = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  constructor(
    private readonly route: ActivatedRoute,
    private readonly candidatesService: CandidatesService,
  ) {}

  ngOnInit(): void {
    this.loadCandidate();
  }

  loadCandidate(): void {
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

  onTriggerAction(action: string): void {
    const current = this.candidate();
    if (!current) return;

    this.actionLoading.set(action);
    this.actionSuccess.set(null);
    this.actionError.set(null);

    this.candidatesService.triggerLifecycleAction(current.id, action).subscribe({
      next: (res) => {
        this.actionLoading.set(null);
        this.actionSuccess.set(res.message || `Event '${action}' published to Azure Event Grid.`);
        // Update local candidate status
        this.candidate.set({
          ...current,
          status: res.status
        });
      },
      error: (err) => {
        this.actionLoading.set(null);
        const errMsg = err?.error?.message || err?.message || 'Failed to trigger candidate lifecycle action.';
        this.actionError.set(errMsg);
      }
    });
  }

  clearSuccess(): void {
    this.actionSuccess.set(null);
  }

  clearError(): void {
    this.actionError.set(null);
  }

  getStatusBadgeClass(status?: string): string {
    switch (status?.toLowerCase()) {
      case 'shortlisted':
        return 'bg-success';
      case 'completed':
      case 'verificationcompleted':
        return 'bg-primary';
      case 'failed':
      case 'verificationfailed':
        return 'bg-danger';
      case 'offerreleased':
        return 'bg-warning text-dark';
      default:
        return 'bg-secondary';
    }
  }
}
